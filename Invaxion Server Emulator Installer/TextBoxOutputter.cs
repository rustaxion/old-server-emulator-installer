using System;
using System.IO;
using System.Text;
using System.Windows.Controls;

namespace Invaxion_Server_Emulator_Installer
{
    public class TextBoxOutputter : TextWriter
    {
        public TextBlock LogBlock = null;

        public TextBoxOutputter(TextBlock output)
        {
            LogBlock = output;
        }

        public override void Write(char value)
        {
            base.Write(value);
            LogBlock.Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    LogBlock.Text += value.ToString();
                })
            );
        }

        public override Encoding Encoding => System.Text.Encoding.UTF8;
    }
}
