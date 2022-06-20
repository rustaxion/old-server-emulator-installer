using System;
using System.IO;
using System.Text;
using System.Windows.Controls;

namespace Invaxion_Server_Emulator_Installer
{
    public class TextBoxOutputter : TextWriter
    {
        private TextBlock LogBlock = null;

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

        public override Encoding Encoding
        {
            get { return System.Text.Encoding.UTF8; }
        }
    }
}
