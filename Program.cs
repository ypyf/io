using System;
using System.Collections;
using io;

namespace ConsoleApplication
{
    public class Program
    {
        public static void Main(string[] args)
        {
            IoState state = new IoState();
            state.prompt(state);
        }
    }
}
