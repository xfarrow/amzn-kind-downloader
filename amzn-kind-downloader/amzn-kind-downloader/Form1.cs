namespace amzn_kind_downloader
{
    public partial class amzn_kind_downloader : Form
    {
        public amzn_kind_downloader()
        {
            InitializeComponent();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (bookLink_TxtBx.Text.Equals(String.Empty) || cookiesPath_TxtBx.Text.Equals(String.Empty) || saveIn_TxtBx.Text.Equals(String.Empty))
            {
                MessageBox.Show("Please, fill the boxes", "Attention required", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            Program.StartExecution(bookLink_TxtBx.Text, cookiesPath_TxtBx.Text, saveIn_TxtBx.Text);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            DialogResult dr = openFileDialog1.ShowDialog();
            if (dr == DialogResult.OK)
            {
                cookiesPath_TxtBx.Text = openFileDialog1.FileName;
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            DialogResult dr = folderBrowserDialog1.ShowDialog();
            if (dr == DialogResult.OK)
            {
                saveIn_TxtBx.Text = folderBrowserDialog1.SelectedPath;
            }
        }
    }
}