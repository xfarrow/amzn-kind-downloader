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
            Program.StartExecution(bookLink_TxtBx.Text, cookiesPath_TxtBx.Text, saveIn_TxtBx.Text);

        }
    }
}