using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SoftRouter
{
	static class Program
	{
		/// <summary>
		/// 应用程序的主入口点。
		/// </summary>
		[STAThread]
		static void Main()
		{
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
            // 设置全局异常处理
            Application.ThreadException += new ThreadExceptionEventHandler(GlobalExceptionHandler);
            Application.Run(new MainForm());
		}
        static void GlobalExceptionHandler(object sender, ThreadExceptionEventArgs e)
        {
            MessageBox.Show("发生未处理的异常：" + e.Exception.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
