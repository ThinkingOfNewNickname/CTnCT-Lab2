namespace PlagiarismChecker
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
        }

        private void StartButton_Click(object sender, EventArgs e)
        {
            string[] fileEntries = Directory.GetFiles("Original Texts");

            int sensativity = (int) sensativityNumericUpDown.Value;
            int minWordsInSentence = (int) minWordInSentenceNumericUpDown.Value;
            int maxQuoteLength = (int) maxWordsInQuoteNumericUpDown.Value;
            
            TextHandler textHandler = new(sensativity, minWordsInSentence, maxQuoteLength);

            foreach (string fileName in fileEntries)
            {
                textHandler.HandleOriginText(fileName);
            }

            string[] output = textHandler.CheckTextOnPlagiarism(inputRichTextBox.Lines);

            resultRichTextBox.Lines = output;
        }
    }
}