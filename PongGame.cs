using Krane.Core;
using Krane.Extensions;
using Krane.Resources;
using SFML.Graphics;
using SFML.System;
using System.Reflection;
using System.Text;

namespace Pong;
internal class PongGame() : Game((WIDTH, HEIGHT), "Pong Game")
{
    public new static uint WIDTH = 640, HEIGHT = 480;
    Pong Pong1 = new();
    public override void Initialize()
    {
        FontManager.Active = new Font(ReadEmbedded("pong-score.ttf"));
        Pong1.Reset();
    }
    public override void Update()
    {
        Pong1.Update();
    }
    public override void Draw()
    {
        Render.Clear(new Color(0,0,0,10));
        Pong1.Draw();
    }
    byte[] ReadEmbedded(string filename)
    {
        var info = Assembly.GetExecutingAssembly().GetName();
        var name = info.Name;
        using var stream = Assembly
            .GetExecutingAssembly()
            .GetManifestResourceStream($"{name}.Embedded.{filename}")!;
        var memStream = new MemoryStream();
        stream.CopyTo(memStream);
        return memStream.ToArray();
    }
}
class Pong
{
    public enum Direction
    {
        Up = -1,
        None = 0,
        Down = 1
    }
    public static class StateIndexTable
    {
        public static int 
            PLAYERID = 0,
            BALLPOSX = 1,
            BALLPOSY = 2,
            BALLVELX = 3,
            BALLVELY = 4,
            P1POSY = 5,
            P2POSY = 6;
    }
    public abstract class Player
    {
        public static float PaddleSpeed => 200 * GameTime.DeltaTime.AsSeconds();
        public static Vector2f PaddleSize => new(15, 90);
        public float X { get; set; }
        public float Y { get; set; }
        public int Score = 0;
        public RectangleShape Paddle => new RectangleShape(PaddleSize)
        {
            Position = new Vector2f(X, Y),
            Origin = PaddleSize/2,
        };
        private readonly int playerID;
        public Player(int playerID)
        {
            this.playerID = playerID;
            Reset();
        }
        public void Reset()
        {
            Score = 0;
            X = playerID == 0 ? 1.5f* PaddleSize.X : PongGame.WIDTH - 1.5f * PaddleSize.X;
            Y = PongGame.HEIGHT / 2;
        }
        public void Go(Direction dir)
        {
            switch (dir)
            {
                case Direction.Up:
                    if (Y >= PaddleSize.Y/2)
                        Y -= PaddleSpeed;
                    break;
                case Direction.Down:
                    if (Y <= PongGame.HEIGHT - PaddleSize.Y / 2)
                        Y += PaddleSpeed;
                    break;
            }
        }
        public abstract Direction MoveDirection(float[] states);
        public void Update(float[] states)
        {
            Go(MoveDirection(states));
        }
        public void Draw()
        {
            Render.Draw(Paddle);
            //Render.Draw(new LineShape(new Vector2f(0, Y), new Vector2f(PongGame.WIDTH, Y)));
        }
    }
    class HumanPlayer(int playerID) : Player(playerID)
    {
        public override Direction MoveDirection(float[] states)
        {
            int dir = 0;
            if (Input.IsKeyDown(playerID == 0 ? SFML.Window.Keyboard.Key.W : SFML.Window.Keyboard.Key.Up))
                dir -= 1;
            if (Input.IsKeyDown(playerID == 0 ? SFML.Window.Keyboard.Key.S : SFML.Window.Keyboard.Key.Down))
                dir += 1;
            return (Direction)dir;
        }
    }
    class DumbAI(int playerID) : Player(playerID)
    {
        public override Direction MoveDirection(float[] states)
        {
            float ballY = states[StateIndexTable.BALLPOSY];
            float playerY = states[playerID == 0 ? StateIndexTable.P1POSY : StateIndexTable.P2POSY];
            if (ballY > playerY)
                return Direction.Down;
            else if (ballY < playerY)
                return Direction.Up;
            else
                return Direction.None;
        }
    }
    class Ball
    {
        public static float SPEED { get; set; } = 200f;
        public static float DIAMETER { get; set; } = 15f;
        public float DifficultyMultiplier { get; set; } = 3f;
        public int Collisions { get; set; }
        public float RelSpeed => (SPEED + Collisions * DifficultyMultiplier) * GameTime.DeltaTime.AsSeconds();
        public float SHIFT_RANGE => 10*RelSpeed;
        public float MAX_SHIFT => 5*RelSpeed;
        public RectangleShape BallShape => new RectangleShape(new Vector2f(DIAMETER, DIAMETER))
        {
            Position = Pos,
        }.Center();
        public Vector2f Pos { get; set; }
        float XDir { get; set; }
        public float XVel => XDir * RelSpeed;
        public float YVel { get; set; }
        public Ball() => Reset();
        public void Reset()
        {
            Collisions = 0;
            Pos = new Vector2f(PongGame.WIDTH / 2, PongGame.HEIGHT / 2);
            XDir = Random.Shared.Next() % 2 == 0 ? -1 : 1;
            YVel = Random.Shared.Next(-5, 5);
        }
        public void Update()
        {
            Pos += new Vector2f(XDir * RelSpeed,YVel);
            if (Pos.Y <= DIAMETER / 2 || Pos.Y >= PongGame.HEIGHT - DIAMETER / 2)
            {
                Pos = new Vector2f(Pos.X, Math.Clamp(Pos.Y, 0, PongGame.HEIGHT));
                YVel = -YVel;
            }
        }
        public void CheckCollisions(Player P1, Player P2)
        {
            var p1Bounds = P1.Paddle.GetGlobalBounds();
            var p2Bounds = P2.Paddle.GetGlobalBounds();
            if (BallShape.GetGlobalBounds().Intersects(p1Bounds))
            {
                float shift = -(P1.Y - Pos.Y)/ P1.Y;
                
                Deflect(shift, P1.X + P1.Paddle.Size.X + 10);
            }
            if (BallShape.GetGlobalBounds().Intersects(p2Bounds))
            {
                float shift = -(P2.Y - Pos.Y)/P2.Y;
                Deflect(shift, p2Bounds.Left - 10);
            }

            if (Pos.X <= 0)
            {
                Reset();
                P2.Score++;
            }
            if (Pos.X >= PongGame.WIDTH)
            {
                Reset();
                P1.Score++;
            }
        }
        public void Draw()
        {
            Render.Draw(BallShape);
        }
        void Deflect(float shift, float X)
        {
            Collisions++;
            Pos = new Vector2f(X, Pos.Y);
            XDir = -XDir;
            YVel = Math.Clamp(YVel + shift * SHIFT_RANGE,-MAX_SHIFT,MAX_SHIFT);
        }
    }

    HumanPlayer P1 = new(0);
    DumbAI P2 = new(1);
    Ball GameBall = new Ball();
    public Pong()
    {
        Reset();
    }
    /*
        PLAYERID = 0,
        BALLPOSX = 1,
        BALLPOSY = 2,
        BALLVELX = 3,
        BALLVELY = 4,
        P1POSY = 5,
        P2POSY = 6;
     */
    public float[] GetStates(int playerID)
    {
        return [playerID,GameBall.Pos.X,GameBall.Pos.Y,GameBall.XVel,GameBall.YVel,P1.Y,P2.Y];
    }
    public void Update()
    {
        P1.Update(GetStates(0));
        P2.Update(GetStates(1));
        GameBall.Update();
        GameBall.CheckCollisions(P1, P2);
    }
    public void Draw()
    {
        var centerSpacing = 50;
        var P1Score = new Text($"{P1.Score}", FontManager.Active,64);
        var P1SB = P1Score.GetLocalBounds();
        P1Score.Position = new Vector2f(PongGame.WIDTH/2 - P1SB.Width - centerSpacing, 10);
        Render.Draw(P1Score);

        var P2Score = new Text($"{P2.Score}", FontManager.Active,64);
        P2Score.Position = new Vector2f(PongGame.WIDTH / 2 + centerSpacing, 10);
        Render.Draw(P2Score);

        Render.Draw(
            new LineShape(
                new Vector2f(PongGame.WIDTH/2,0),
                new Vector2f(PongGame.WIDTH/2,PongGame.HEIGHT),
                thickness:5,
                new Color(255,255,255,50)));

        P1.Draw();
        P2.Draw();
        GameBall.Draw();
    }

    public void Reset()
    {
        P1.Reset();
        P2.Reset();
        GameBall.Reset();
    }
}

