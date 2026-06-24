using SkiaSharp;
using SkiaSharp.Views.Maui;
using System.Diagnostics;

namespace Snailrunner;

public enum GameState { Lobby, Playing, CharacterSelect, Shop, GameOver }

public struct GamePlatform
{
    public SKRect Rect;      
    public SKColor Color;    
}

public class GameItem
{
    public SKRect Rect { get; set; }
    public string Type { get; set; } = string.Empty;
}

public class PlayerBullet
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Speed { get; set; } = 850f; 
    public float Direction { get; set; } = 1f; 
}

public partial class MainPage : ContentPage
{
    private GameState currentState = GameState.Lobby;
    private bool isPaused = false; 
    private float cameraX = 0f;

    private int playerCurrency = 10; 
    private int globalWallet = 10;    

    private float playerX = 200;
    private float playerY = 300; 
    private float playerSpeedX = 380f; 
    private float playerHeight = 55f;     
    private float playerWidth = 30f;      
    private float playerVelocityY = 0f;   
    private const float gravity = 1800f;  
    private const float jumpForce = -720f; 
    private bool isGrounded = false;      
    private SKColor playerColor = SKColors.DeepSkyBlue;
    private float playerFacingDir = 1f; 

    private List<PlayerBullet> bullets = new List<PlayerBullet>();
    private List<GamePlatform> platforms = new List<GamePlatform>(); 
    private List<GameItem> gameItems = new List<GameItem>(); 

    private float maxGeneratedX = 0f; 
    private float lastPlatformTopY = 500f; 
    private Random random = new Random();

    private float demonX = 0f;
    private float demonSpeed = 125f; 

    private bool isLeftPressed = false;
    private bool isRightPressed = false;
    private bool isJumpPressed = false;
    private bool isCrouchPressed = false;

    private IDispatcherTimer gameTimer;
    private Stopwatch stopwatch;
    private float lastElapsedTime;

    public MainPage()
    {
        InitializeComponent();

        stopwatch = new Stopwatch();
        stopwatch.Start();

        gameTimer = Dispatcher.CreateTimer();
        gameTimer.Interval = TimeSpan.FromMilliseconds(1); 
        gameTimer.Tick += GameLoop;
        gameTimer.Start();

        Appearing += OnMainPageAppearing;
        SwitchState(GameState.Lobby);
    }

    private void SpawnNextPlatform()
    {
        if (maxGeneratedX == 0f)
        {
            platforms.Add(new GamePlatform { Rect = new SKRect(0, 500, 1000, 600), Color = SKColor.Parse("#3E2723") });
            maxGeneratedX = 1000f;
            lastPlatformTopY = 500f;
            return;
        }

        float gap = random.Next(110, 230); 
        float width = random.Next(250, 600);
        float startX = maxGeneratedX + gap;
        float endX = startX + width;

        float maxJumpableDiff = 120f; 
        float targetY = lastPlatformTopY + (float)(random.NextDouble() * 2.0 - 1.0) * maxJumpableDiff;

        float topY = Math.Clamp(targetY, 280f, 520f);
        float bottomY = topY + 120f; 

        platforms.Add(new GamePlatform 
        { 
            Rect = new SKRect(startX, topY, endX, bottomY), 
            Color = SKColor.Parse(topY >= 480f ? "#3E2723" : "#795548") 
        });

        bool hasCeiling = false;
        float ceilStartX = 0f;
        float ceilEndX = 0f;

        if (width > 450 && random.Next(0, 100) < 25)
        {
            hasCeiling = true;
            float ceilingHeight = 40f; 
            float ceilingThickness = 100f;
            
            float ceilingTopY = topY - ceilingHeight - ceilingThickness;
            float ceilingBottomY = topY - ceilingHeight;
            
            ceilStartX = startX + 130f;
            ceilEndX = endX - 130f;

            platforms.Add(new GamePlatform
            {
                Rect = new SKRect(ceilStartX, ceilingTopY, ceilEndX, ceilingBottomY),
                Color = SKColor.Parse("#212121") 
            });
        }

        bool spawnedDoor = false;
        if (!hasCeiling && random.Next(0, 100) < 5)
        {
            float doorWidth = 40f;
            float doorHeight = 70f;
            float doorX = startX + (width / 2f) - (doorWidth / 2f); 
            float doorY = topY - doorHeight;

            gameItems.Add(new GameItem 
            { 
                Rect = new SKRect(doorX, doorY, doorX + doorWidth, doorY + doorHeight), 
                Type = "Door" 
            });
            spawnedDoor = true;
        }

        if (!spawnedDoor)
        {
            int itemsToSpawn = random.Next(1, 4); 
            for (int i = 0; i < itemsToSpawn; i++)
            {
                float itemX = startX + random.Next(30, (int)width - 40);

                if (hasCeiling && itemX >= (ceilStartX - 20f) && itemX <= (ceilEndX + 20f))
                {
                    continue;
                }

                float itemY = topY - 25f; 

                int roll = random.Next(0, 100);
                if (roll < 75) 
                {
                    gameItems.Add(new GameItem { Rect = new SKRect(itemX, itemY + 5, itemX + 15, itemY + 20), Type = "Coin" });
                }
                else 
                {
                    gameItems.Add(new GameItem { Rect = new SKRect(itemX, itemY + 5, itemX + 25, itemY + 25), Type = "Hazard" });
                }
            }
        }

        maxGeneratedX = endX;
        lastPlatformTopY = topY;
    }

    private void OnMainPageAppearing(object? sender, EventArgs e)
    {
#if WINDOWS
        var window = Window.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
        if (window?.Content != null)
        {
            window.Content.KeyDown += (s, args) =>
            {
                var key = args.Key;
                if (currentState == GameState.Playing && !isPaused)
                {
                    if (key == Windows.System.VirtualKey.A || key == Windows.System.VirtualKey.Left) isLeftPressed = true;
                    if (key == Windows.System.VirtualKey.D || key == Windows.System.VirtualKey.Right) isRightPressed = true;
                    if (key == Windows.System.VirtualKey.W || key == Windows.System.VirtualKey.Up) isJumpPressed = true;
                    if (key == Windows.System.VirtualKey.S || key == Windows.System.VirtualKey.Down) isCrouchPressed = true;
                    if (key == Windows.System.VirtualKey.Space) OnShootClicked(this, EventArgs.Empty); 
                }
            };

            window.Content.KeyUp += (s, args) =>
            {
                var key = args.Key;
                if (key == Windows.System.VirtualKey.A || key == Windows.System.VirtualKey.Left) isLeftPressed = false;
                if (key == Windows.System.VirtualKey.D || key == Windows.System.VirtualKey.Right) isRightPressed = false;
                if (key == Windows.System.VirtualKey.W || key == Windows.System.VirtualKey.Up) isJumpPressed = false;
                if (key == Windows.System.VirtualKey.S || key == Windows.System.VirtualKey.Down) isCrouchPressed = false;
            };
        }
#endif
    }

    private void SwitchState(GameState newState)
    {
        currentState = newState;

        LobbyMenu.IsVisible = (currentState == GameState.Lobby);
        GameHUD.IsVisible = (currentState == GameState.Playing);
        GameOverMenu.IsVisible = (currentState == GameState.GameOver); 

        if (currentState == GameState.Lobby)
        {
            LobbyWalletLabel.Text = $"Bank: {globalWallet} 🪙";
        }
        else if (currentState == GameState.GameOver)
        {
            bullets.Clear();
            gameItems.Clear();
        }

        if (currentState != GameState.Playing)
        {
            isLeftPressed = false; isRightPressed = false; isJumpPressed = false; isCrouchPressed = false;
        }
    }

    private void HandleDoorExit()
    {
        isPaused = true;
        globalWallet = playerCurrency; 
        playerCurrency = 0;
        
        Dispatcher.DispatchDelayed(TimeSpan.FromSeconds(0.5), () => 
        {
            SwitchState(GameState.Lobby);
        });
    }

    private void HandleHardcoreDeath()
    {
        globalWallet = 0;    
        playerCurrency = 0;  
        SwitchState(GameState.GameOver);
    }

    private void GameLoop(object? sender, EventArgs e)
    {
        float currentTime = (float)stopwatch.Elapsed.TotalSeconds;
        float deltaTime = currentTime - lastElapsedTime;
        lastElapsedTime = currentTime;

        if (currentState == GameState.Playing && !isPaused)
        {
            float feetY = playerY + playerHeight;
            bool ceilingAbove = false;

            SKRect standingCheckRect = new SKRect(playerX + 1f, feetY - 55f, playerX + playerWidth - 1f, feetY - 5f);
            foreach (var platform in platforms)
            {
                if (standingCheckRect.IntersectsWith(platform.Rect))
                {
                    ceilingAbove = true;
                    break;
                }
            }

            float targetHeight = (isCrouchPressed || ceilingAbove) ? 25f : 55f;
            if (playerHeight != targetHeight)
            {
                playerY = feetY - targetHeight;
                playerHeight = targetHeight;
            }

            float deltaX = 0f;
            if (isLeftPressed) { deltaX -= playerSpeedX * deltaTime; playerFacingDir = -1f; }
            if (isRightPressed) { deltaX += playerSpeedX * deltaTime; playerFacingDir = 1f; }

            playerX += deltaX;

            foreach (var platform in platforms)
            {
                SKRect playerBoundsH = new SKRect(playerX, playerY, playerX + playerWidth, playerY + playerHeight);
                if (playerBoundsH.IntersectsWith(platform.Rect))
                {
                    if (deltaX > 0) playerX = platform.Rect.Left - playerWidth;
                    else if (deltaX < 0) playerX = platform.Rect.Right;
                }
            }

            while (maxGeneratedX < cameraX + 2000f) SpawnNextPlatform();
            platforms.RemoveAll(p => p.Rect.Right < cameraX - 600f);
            gameItems.RemoveAll(item => item.Rect.Right < cameraX - 600f);

            for (int i = bullets.Count - 1; i >= 0; i--)
            {
                bullets[i].X += bullets[i].Speed * bullets[i].Direction * deltaTime;
                if (bullets[i].X > cameraX + 1500f || bullets[i].X < cameraX - 500f) bullets.RemoveAt(i);
            }

            playerVelocityY += gravity * deltaTime;
            playerY += playerVelocityY * deltaTime;
            
            isGrounded = false;

            foreach (var platform in platforms)
            {
                SKRect playerBoundsV = new SKRect(playerX, playerY, playerX + playerWidth, playerY + playerHeight);
                if (playerBoundsV.IntersectsWith(platform.Rect))
                {
                    if (playerVelocityY >= 0)
                    {
                        playerY = platform.Rect.Top - playerHeight;
                        playerVelocityY = 0f;
                        isGrounded = true;
                    }
                    else if (playerVelocityY < 0)
                    {
                        playerY = platform.Rect.Bottom;
                        playerVelocityY = 0f;
                    }
                }
            }

            if (isJumpPressed && isGrounded && playerHeight == 55f)
            {
                playerVelocityY = jumpForce;
                isGrounded = false;
            }

            if (playerX - cameraX > 400f) cameraX = playerX - 400f;
            if (playerX < cameraX) playerX = cameraX; 

            SKRect finalPlayerBounds = new SKRect(playerX, playerY, playerX + playerWidth, playerY + playerHeight);
            for (int i = gameItems.Count - 1; i >= 0; i--)
            {
                if (finalPlayerBounds.IntersectsWith(gameItems[i].Rect))
                {
                    var item = gameItems[i];
                    if (item.Type == "Coin")
                    {
                        playerCurrency++; 
                        gameItems.RemoveAt(i);
                    }
                    else if (item.Type == "Door")
                    {
                        HandleDoorExit();
                        return; 
                    }
                    else if (item.Type == "Hazard")
                    {
                        HandleHardcoreDeath();
                        return; 
                    }
                }
            }

            demonX += demonSpeed * deltaTime;

            if (playerX <= demonX) 
            {
                HandleHardcoreDeath();
                return;
            }

            if (playerY > 700) 
            {
                HandleHardcoreDeath();
                return;
            }

            HUDCurrencyLabel.Text = $"🪙 TŐKE/LŐSZER: {playerCurrency}";
        }

        GameCanvas.InvalidateSurface();
    }

    public void OnLeftPressed(object sender, EventArgs e) => isLeftPressed = true;
    public void OnLeftReleased(object sender, EventArgs e) => isLeftPressed = false;
    public void OnRightPressed(object sender, EventArgs e) => isRightPressed = true;
    public void OnRightReleased(object sender, EventArgs e) => isRightPressed = false;
    public void OnJumpPressed(object sender, EventArgs e) => isJumpPressed = true;
    public void OnJumpReleased(object sender, EventArgs e) => isJumpPressed = false;
    public void OnCrouchPressed(object sender, EventArgs e) => isCrouchPressed = true;
    public void OnCrouchReleased(object sender, EventArgs e) => isCrouchPressed = false;

    public void OnShootClicked(object sender, EventArgs e)
    {
        if (currentState == GameState.Playing && !isPaused && playerCurrency > 0)
        {
            playerCurrency--; 
            float spawnX = playerFacingDir > 0 ? (playerX + playerWidth) : (playerX - 16f);

            bullets.Add(new PlayerBullet 
            { 
                X = spawnX, 
                Y = playerY + (playerHeight / 2) - 3f,
                Direction = playerFacingDir 
            });
        }
    }

    public void OnStartGameClicked(object sender, EventArgs e)
    {
        playerCurrency = globalWallet; 

        playerX = 200; playerY = 300;
        cameraX = 0; demonX = -100;
        playerVelocityY = 0f;
        isGrounded = false; isPaused = false;
        playerFacingDir = 1f;
        
        bullets.Clear(); 
        gameItems.Clear();
        maxGeneratedX = 0f;
        lastPlatformTopY = 500f; 
        platforms.Clear();
        SpawnNextPlatform(); 

        PauseButton.Text = "⏸️ SZÜNET";
        SwitchState(GameState.Playing);
    }

    public void OnPauseClicked(object sender, EventArgs e)
    {
        if (currentState != GameState.Playing) return;
        isPaused = !isPaused;
        PauseButton.Text = isPaused ? "▶️ FOLYTATÁS" : "⏸️ SZÜNET";
    }

    public void OnCharacterMenuClicked(object sender, EventArgs e) {}
    public void OnShopMenuClicked(object sender, EventArgs e) {}
    public void OnBackToLobbyClicked(object sender, EventArgs e) => SwitchState(GameState.Lobby);
    public void OnExitToLobbyClicked(object sender, EventArgs e) => SwitchState(GameState.Lobby);

    private void OnCanvasPaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        SKCanvas canvas = e.Surface.Canvas;
        canvas.Clear(SKColor.Parse("#0D0D11"));

        using (SKPaint paint = new SKPaint { IsAntialias = true })
        {
            if (currentState == GameState.Playing || currentState == GameState.GameOver)
            {
                canvas.Save();
                canvas.Translate(-cameraX, 0);

                foreach (var platform in platforms)
                {
                    paint.Color = platform.Color;
                    if (platform.Color.ToString() == "#FF212121")
                        canvas.DrawRoundRect(platform.Rect, 5, 5, paint);
                    else
                        canvas.DrawRect(platform.Rect, paint);
                    
                    if (platform.Rect.Height > 100)
                    {
                        paint.Color = SKColor.Parse("#B71C1C"); 
                        canvas.DrawRect(platform.Rect.Left, platform.Rect.Top, platform.Rect.Width, 6, paint);
                    }
                }

                foreach (var item in gameItems)
                {
                    if (item.Type == "Coin")
                    {
                        paint.Color = SKColors.Gold;
                        canvas.DrawOval(item.Rect, paint); 
                    }
                    else if (item.Type == "Hazard")
                    {
                        paint.Color = SKColors.OrangeRed;
                        using (SKPath path = new SKPath())
                        {
                            path.MoveTo(item.Rect.Left, item.Rect.Bottom);
                            path.LineTo(item.Rect.MidX, item.Rect.Top);
                            path.LineTo(item.Rect.Right, item.Rect.Bottom);
                            path.Close();
                            canvas.DrawPath(path, paint);
                        }
                    }
                    else if (item.Type == "Door")
                    {
                        paint.Color = SKColor.Parse("#5D4037"); 
                        canvas.DrawRect(item.Rect, paint);
                        paint.Color = SKColors.Gold; 
                        canvas.DrawCircle(item.Rect.Right - 8, item.Rect.MidY, 4, paint);
                        paint.Style = SKPaintStyle.Stroke;
                        paint.Color = SKColor.Parse("#3E2723");
                        canvas.DrawRect(item.Rect, paint); 
                        paint.Style = SKPaintStyle.Fill;
                    }
                }

                paint.Color = SKColors.Yellow;
                foreach (var bullet in bullets) canvas.DrawRect(bullet.X, bullet.Y, 16f, 5f, paint); 

                paint.Color = playerColor;
                canvas.DrawRect(playerX, playerY, playerWidth, playerHeight, paint);

                paint.Color = SKColor.Parse("#220000");
                canvas.DrawRect(demonX - 600, 0, 600, (float)GameCanvas.Height, paint);
                paint.Color = SKColors.Red; paint.StrokeWidth = 5f; paint.Style = SKPaintStyle.Stroke;
                canvas.DrawLine(demonX, 0, demonX, (float)GameCanvas.Height, paint);
                paint.Style = SKPaintStyle.Fill;

                canvas.Restore();

                if (currentState == GameState.Playing)
                {
                    paint.Color = SKColor.Parse("#AA000000");
                    canvas.DrawRoundRect(20, 70, 240, 35, 4, 4, paint);
                    paint.TextSize = 14; paint.Color = SKColors.White;
                    canvas.DrawText($"😈 Démon távolság: {(int)(playerX - demonX)} m", 35, 92, paint);
                }

                if (isPaused && currentState == GameState.Playing)
                {
                    paint.Color = SKColor.Parse("#CC000000");
                    canvas.DrawRect(0, 0, (float)GameCanvas.Width, (float)GameCanvas.Height, paint);
                    paint.Color = SKColors.Red; paint.TextSize = 36; paint.FakeBoldText = true;
                    canvas.DrawText("JÁTÉK MEGSZAKÍTVA", (float)GameCanvas.Width / 2 - 160, (float)GameCanvas.Height / 2, paint);
                }
            }
        }
    }
}