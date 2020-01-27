using System;
using System.Globalization;
using System.Threading;

namespace SampleLib
{
    public static class SampleClass
    {
        public static int Method(string[] args)
        {
            if (args == null)
                throw new ArgumentNullException(nameof(args));

            Console.WriteLine($"{nameof(Method)}({string.Join(", ", args)})");
            int result = 0;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "crash")
                {
                    Environment.Exit(666);
                }

                if (args[i] == "throw")
                {
                    throw new Exception("Exception forced by arguments");
                }

                if (args[i].StartsWith("sleep", StringComparison.Ordinal))
                {
                    int value = int.Parse(args[i].Substring("sleep".Length), CultureInfo.InvariantCulture);
                    Thread.Sleep(value);
                }

                if (args[i].StartsWith("return", StringComparison.Ordinal))
                {
                    result = int.Parse(args[i].Substring("return".Length), CultureInfo.InvariantCulture);
                }
            }

            Console.WriteLine($"{nameof(Method)} = {result}");
            return 0;
        }
    }
}
