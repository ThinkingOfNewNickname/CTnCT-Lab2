using System.Collections;
using System.Text;
using System.Text.RegularExpressions;

namespace PlagiarismChecker
{
    public class TextHandler
    {
        private const int pForChar = 41;
        private const int mForChar = 2825773;

        private const int powsAmountForChar = 5;

        private readonly int[] pPowsForChar = new int[powsAmountForChar];


        private const long pForWord = 2825777;
        private const long mForWord = 4223372036854775803;

        private const long powsAmountForWord = 25;

        private readonly long[] pPowsForWord = new long[powsAmountForWord];

        private readonly List<string> originals = new();

        private readonly Hashtable hashedSentences = new();
        private readonly Hashtable hashedWordSequences = new();

        private readonly int sensitivity;
        private readonly int minWordsInSentence;
        private readonly int maxQuoteLength;


        public TextHandler(int sensitivity, int minWordsInSentence, int maxQuoteLength)
        {
            this.sensitivity = sensitivity;
            this.minWordsInSentence = minWordsInSentence;
            this.maxQuoteLength = maxQuoteLength;

            InitCoefficients();
        }

        public void HandleOriginText(string path)
        {
            int originalIndex = originals.Count;
            originals.Add(Path.GetFileName(path));

            string[] lines = File.ReadAllLines(path);

            lines = CombineStrings(lines);

            lines = RemoveUnnecessarySpaces(lines);

            string[] sentences = SeparateByDotsAndQuestionMarksAndExclamationMarks(lines);

            sentences = CleanUp(sentences);

            int[][] allWordHashes = ComputeHashPerEachWordForAllSentences(sentences);

            CalculateAndAddHashForSentences(allWordHashes, originalIndex);
            CalculateAndAddHashForWordSequences(allWordHashes, originalIndex);
        }

        public string[] CheckTextOnPlagiarism(string[] inputText)
        {
            string[] lines = inputText;

            lines = CombineStrings(lines);

            lines = RemoveUnnecessarySpaces(lines);

            string[] sentences = SeparateByQuotes(lines);

            ExtractQuotes(sentences, out string[] quotes, out string[] noQuotes);

            noQuotes = SeparateByDotsAndQuestionMarksAndExclamationMarks(noQuotes);

            noQuotes = CleanUp(noQuotes);

            int[][] allWordHashes = ComputeHashPerEachWordForAllSentences(noQuotes);

            int wordAmount = WordAmount(quotes) + WordAmount(noQuotes);

            float[] sentenceMatch = new float[originals.Count];
            float[] wordSequenceMatch = new float[originals.Count];

            Array.Fill(sentenceMatch, 0.0f);
            Array.Fill(wordSequenceMatch, 0.0f);

            CheckSentencesOnPlagiarism(allWordHashes, noQuotes, wordAmount, sentenceMatch);
            CheckWordSequencesOnPlagiarism(allWordHashes, noQuotes, wordAmount, wordSequenceMatch);

            List<string> output = new();

            output.Add("Sentence Matching: ");

            for (int i = 0; i < originals.Count; i++)
            {
                if (sentenceMatch[i] > 0.000001)
                {
                    output.Add(string.Format("{0} : {1} %", originals[i], (sentenceMatch[i] * 100.0).ToString("0.0000")));
                }
            }

            output.Add("");
            output.Add("Word Sequence Matching: ");
            for (int i = 0; i < originals.Count; i++)
            {
                if (wordSequenceMatch[i] > 0.000001)
                {
                    output.Add(string.Format("{0} : {1} %", originals[i], (wordSequenceMatch[i] * 100.0).ToString("0.0000")));
                }
            }

            return output.ToArray();
        }

        private void InitCoefficients()
        {
            int previousPowForChar = 1;

            for (int i = 0; i < powsAmountForChar; ++i)
            {
                pPowsForChar[i] = previousPowForChar;

                previousPowForChar = (previousPowForChar * pForChar) % mForChar;
            }


            long previousPowForWord = 1;

            for (int i = 0; i < powsAmountForWord; ++i)
            {
                pPowsForWord[i] = previousPowForWord;

                previousPowForWord = (previousPowForWord * pForWord) % mForWord;
            }
        }


        private static int CharToInt(char c)
        {
            if (c >= '0' && c <= '9')
            {
                return c - '0';
            }

            if (c >= 'a' && c <= 'z')
            {
                const int offsetToPutAfterNumbers = 10;
                return c - 'a' + offsetToPutAfterNumbers;
            }

            const int offsetToPutAfterLetters = 10 + 26;
            return offsetToPutAfterLetters;
        }

        private int ComputeHashForWord(string word)
        {
            int hash = 0;

            for (int i = 0; i < word.Length; ++i)
            {
                hash = (hash + CharToInt(word[i]) * pPowsForChar[powsAmountForChar - 1 - (i % powsAmountForChar)]) % mForChar;
            }

            return hash;
        }

        private long ComputeHashForWordHashes(int[] wordHashes)
        {
            long hash = 0;

            for (int i = 0; i < wordHashes.Length; ++i)
            {
                hash = (hash + wordHashes[i] * pPowsForWord[powsAmountForWord - 1 - (i % powsAmountForWord)]) % mForWord;
            }

            return hash;
        }

        private static int WordAmount(string[] sentences)
        {
            int wordAmount = 0;
            foreach (string sentence in sentences)
            {
                wordAmount += WordAmount(sentence);
            }

            return wordAmount;
        }

        private static int WordAmount(string sentence)
        {
            return sentence.Count(char.IsWhiteSpace) + 1;
        }

        private static string[] CombineStrings(string[] lines)
        {
            List<string> combined = new();

            StringBuilder previousText = new();

            foreach (string line in lines)
            {
                bool isContainsLetters = line.Any(x => !char.IsLetter(x));

                if (isContainsLetters)
                {
                    previousText.Append(' ');
                    previousText.Append(line);
                }
                else
                {
                    if (previousText.Length > 0)
                    {
                        combined.Add(previousText.ToString());
                        previousText.Clear();
                    }
                }
            }
            if (previousText.Length > 0)
            {
                combined.Add(previousText.ToString());
                previousText.Clear();
            }

            return combined.ToArray();
        }

        private static string[] RemoveUnnecessarySpaces(string[] lines)
        {
            Regex regex = new(@"\s+");

            for (int i = 0; i < lines.Length; ++i)
            {
                string line = lines[i];

                line = regex.Replace(line, " ");

                lines[i] = line;
            }

            return lines;
        }

        private static string[] SeparateByDotsAndQuestionMarksAndExclamationMarks(string[] lines)
        {
            char[] delimiterChars = { '.', '!', '?' };

            List<string> newStrings = new();

            foreach (string line in lines)
            {
                string[] separatedLines = line.Split(delimiterChars);

                foreach (string separatedLine in separatedLines)
                {
                    bool isContainsLetters = separatedLine.Any(x => char.IsLetter(x));
                    if (isContainsLetters)
                    {
                        newStrings.Add(separatedLine);
                    }
                }
            }

            return newStrings.ToArray();
        }

        private static string[] SeparateByQuotes(string[] lines)
        {
            List<string> newStrings = new();

            foreach (string line in lines)
            {
                string[] separatedLines = line.Split('"');

                for (int i = 0; i < separatedLines.Length; ++i)
                {
                    newStrings.Add(separatedLines[i]);
                    if (i < separatedLines.Length - 1)
                    {
                        newStrings.Add("\"");
                    }
                }
            }

            return newStrings.ToArray();
        }

        private void ExtractQuotes(in string[] lines, out string[] quotes, out string[] noQuotes)
        {
            List<string> quoteList = new();
            List<string> noQuoteList = new();
            List<string> undefinedLines = new();

            bool isQuoteStarted = false;
            int wordAmountInsideQuote = 0;

            foreach (string line in lines)
            {
                if (line.CompareTo("\"") == 0)
                {
                    if (isQuoteStarted)
                    {
                        quoteList.AddRange(undefinedLines);
                        undefinedLines.Clear();
                        isQuoteStarted = false;
                        continue;
                    }

                    isQuoteStarted = true;
                    wordAmountInsideQuote = 0;
                    continue;
                }

                if (isQuoteStarted)
                {
                    wordAmountInsideQuote += line.Count(char.IsWhiteSpace) + 1;

                    if (wordAmountInsideQuote > maxQuoteLength)
                    {
                        isQuoteStarted = false;
                        noQuoteList.AddRange(undefinedLines);
                        noQuoteList.Add(line);
                        undefinedLines.Clear();

                        continue;
                    }

                    undefinedLines.Add(line);
                    continue;
                }

                noQuoteList.Add(line);
            }

            quotes = quoteList.ToArray();
            noQuotes = noQuoteList.ToArray();
        }

        private static string[] CleanUp(string[] lines)
        {
            Regex regex = new("[^a-z0-9. -]");

            for (int i = 0; i < lines.Length; ++i)
            {
                string line = lines[i];

                line = line.ToLower();
                line = line.Trim();

                line = regex.Replace(line, "");

                lines[i] = line;
            }

            return lines;
        }

        private int[][] ComputeHashPerEachWordForAllSentences(string[] sentences)
        {
            int[][] allHashes = new int[sentences.Length][];

            for (int i = 0;i < sentences.Length; ++i)
            {
                allHashes[i] = ComputeHashPerEachWord(sentences[i]);
            }

            return allHashes;
        }

        private int[] ComputeHashPerEachWord(string sentence)
        {
            string[] words = sentence.Split(' ');

            int[] hashes = new int[words.Length];

            for (int i = 0;i < words.Length; ++i)
            {
                hashes[i] = ComputeHashForWord(words[i]);
            }

            return hashes;
        }

        private void CalculateAndAddHashForSentences(int[][] allWordHashes, int originalIndex)
        {
            foreach (int[] wordHashes in allWordHashes)
            {
                if (wordHashes.Length < minWordsInSentence)
                {
                    continue;
                }

                long hash = ComputeHashForWordHashes(wordHashes);

                if (hashedSentences.ContainsKey(hash))
                {
                    continue;
                }

                hashedSentences.Add(hash, originalIndex);
            }
        }

        private void CalculateAndAddHashForWordSequences(int[][] allWordHashes, int originalIndex)
        {
            foreach (int[] wordHashes in allWordHashes)
            {
                if (wordHashes.Length < minWordsInSentence)
                {
                    continue;
                }

                CalculateAndAddHashForWordSequence(wordHashes, originalIndex);
            }
        }

        private void CalculateAndAddHashForWordSequence(int[] wordHashes, int originalIndex)
        {
            long hash = 0;

            if (wordHashes.Length <= sensitivity)
            {
                hash = ComputeHashForWordHashes(wordHashes);

                if (hashedWordSequences.ContainsKey(hash))
                {
                    return;
                }

                hashedWordSequences.Add(hash, originalIndex);

                return;
            }

            for (int i = 0; i < sensitivity; ++i)
            {
                hash = (hash + wordHashes[i] * pPowsForWord[powsAmountForWord - 1 - (i % powsAmountForWord)]) % mForWord;
            }

            for (int i = 0; i < wordHashes.Length - sensitivity; ++i)
            {
                if (!hashedWordSequences.ContainsKey(hash))
                {
                    hashedWordSequences.Add(hash, originalIndex);
                }

                hash = ((hash - wordHashes[i] * pPowsForWord[(sensitivity - 1) % powsAmountForWord]) * pForWord + wordHashes[i + sensitivity]) % mForWord;
            }
        }

        private void CheckSentencesOnPlagiarism(int[][] allWordHashes, string[] sentences, int commonWordsAmount, float[] matchPercentage)
        {
            for (int i = 0; i < allWordHashes.Length; ++i)
            {
                int[] wordHashes = allWordHashes[i];
                string sentence = sentences[i];

                if (wordHashes.Length < minWordsInSentence)
                {
                    continue;
                }

                long hash = ComputeHashForWordHashes(wordHashes);

                if (hashedSentences.ContainsKey(hash))
                {
                    int wordAmount = WordAmount(sentence);

                    int index = (int) hashedSentences[hash];

                    matchPercentage[index] += wordAmount / (float) commonWordsAmount;
                }
            }
        }

        private void CheckWordSequencesOnPlagiarism(int[][] allWordHashes, string[] sentences, int commonWordsAmount, float[] matchPercentage)
        {
            for (int i = 0; i < allWordHashes.Length; ++i)
            {
                int[] wordHashes = allWordHashes[i];

                if (wordHashes.Length < minWordsInSentence)
                {
                    continue;
                }

                CheckWordSequenceOnPlagiarism(wordHashes, sentences[i], commonWordsAmount, matchPercentage);
            }
        }

        private void CheckWordSequenceOnPlagiarism(int[] wordHashes, string sentence, int commonWordsAmount, float[] matchPercentage)
        {
            int wordAmount = WordAmount(sentence);

            long hash = 0;

            if (wordHashes.Length <= sensitivity)
            {
                hash = ComputeHashForWordHashes(wordHashes);

                if (hashedWordSequences.ContainsKey(hash))
                {
                    int index = (int) hashedWordSequences[hash];

                    matchPercentage[index] += wordAmount / (float) commonWordsAmount;
                }
                return;
            }

            int[] wordMatchIndex = new int[wordAmount];
            Array.Fill(wordMatchIndex, -1);

            for (int i = 0; i < sensitivity; ++i)
            {
                hash = (hash + wordHashes[i] * pPowsForWord[powsAmountForWord - 1 - (i % powsAmountForWord)]) % mForWord;
            }

            for (int i = 0; i < wordHashes.Length - sensitivity; ++i)
            {
                if (hashedWordSequences.ContainsKey(hash))
                {
                    int index = (int) hashedWordSequences[hash];

                    for (int j = i; j < i + sensitivity; ++j)
                    {
                        wordMatchIndex[j] = index;
                    }
                }

                hash = ((hash - wordHashes[i] * pPowsForWord[(sensitivity - 1) % powsAmountForWord]) * pForWord + wordHashes[i + sensitivity]) % mForWord;
            }

            int commonMatchCount = wordMatchIndex.Count(c => c != -1);

            if (commonMatchCount > 0)
            {
                for (int i = 0; i < originals.Count; ++i)
                {
                    int matchCount = wordMatchIndex.Count(c => c == i);

                    if (matchCount > 0)
                    {
                        matchPercentage[i] += matchCount / (float) commonMatchCount * wordAmount / commonWordsAmount;
                    }
                }
            }
        }
    }
}
