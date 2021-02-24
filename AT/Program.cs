using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AT
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            var ci = CultureInfo.GetCultureInfo("en-US");

            if (Thread.CurrentThread.CurrentCulture.DisplayName == ci.DisplayName)
            {
                ci = CultureInfo.CreateSpecificCulture("en-US");
                ci.NumberFormat.CurrencyNegativePattern = 1;
                Thread.CurrentThread.CurrentCulture = ci;
                Thread.CurrentThread.CurrentUICulture = ci;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Main());
        }
    }
}
