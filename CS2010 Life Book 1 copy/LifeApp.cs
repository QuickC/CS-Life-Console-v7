using System;
using System.Threading;

namespace CS_Life_Console_v5
{
    internal static class LifeApp
    {
        public static void Main()
        {
            ConsoleKeyInfo keyPressed = new ConsoleKeyInfo();
            GameOfLife LifeSim = GameOfLife.LifeClass_s;

            Console.SetWindowSize(85, 28);
            Console.Title = "Life in C# 1024 x 1024";

            do
            {
                if (Console.KeyAvailable)
                {
                    keyPressed = Console.ReadKey(true);     // load the key into the structure

                    switch (keyPressed.Key)
                    {
                        case ConsoleKey.A:          // a = left move of the window, then bounds check
                            LifeSim.ChangeViewBy(-64, 0);
                            break;
                        case ConsoleKey.Add:        // + key single step one tick
                            LifeSim.NextMode = LMode.STEP;
                            break;
                        case ConsoleKey.C:          // c key clear board
                            LifeSim.NextMode = LMode.CLEAR;
                            break;
                        case ConsoleKey.D:          // d = right move of the window, then bounds check
                            LifeSim.ChangeViewBy(64, 0);
                            break;
                        case ConsoleKey.G:          // g for glider
                            LifeSim.NextMode = LMode.ADD_GLIDER;
                            break;
                        case ConsoleKey.L:          // l for large group
                            LifeSim.NextMode = LMode.ADD_LARGE;
                            break;
                        case ConsoleKey.Q:          //enter key exit while
                            LifeSim.NextMode = LMode.EXIT;
                            break;
                        case ConsoleKey.R:          // r for random add
                            LifeSim.NextMode = LMode.ADD_RANDOM;
                            break;
                        case ConsoleKey.S:          // s for small group
                            LifeSim.NextMode = LMode.ADD_SMALL;
                            break;
                        case ConsoleKey.Spacebar:   // 'space run toggle single and start or stop the stopwatch timer
                            LifeSim.NextMode = LifeSim.NextMode == LMode.AUTO ? LMode.IDLE : LMode.AUTO;
                            break;
                        case ConsoleKey.W:          // w = up move of the window, then bounds check
                            LifeSim.ChangeViewBy(0, -16);
                            break;
                        case ConsoleKey.X:          // x = down move of the window, then bounds check
                            LifeSim.ChangeViewBy(0, 16);
                            break;

                        default:
                            break;
                    }
                }

                Console.SetCursorPosition(0, 0);
                Console.Write(LifeSim.DrawView());
                Thread.Sleep(50);
            } while (LifeSim.NextMode != LMode.EXIT);                // loop until run is false
        } // end main
    } //end class
} //end name       