using System;
using System.Runtime.CompilerServices;
using ServiceStack.Logging;

namespace ConsoleApp1
{
    public static class Log
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Info(string message)
        {
            var now = DateTime.Now;
            Console.WriteLine($"[{now.ToShortDateString()} {now.ToLongTimeString()}] {message}");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Error(string message)
        {
            var now = DateTime.Now;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[{now.ToShortDateString()} {now.ToShortTimeString()}] {message}");
            Console.ForegroundColor = ConsoleColor.White;
        }
    }
}