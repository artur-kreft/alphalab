using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MarketData.Tests.Integration
{
    public class MessageTests
    {
        private List<List<string>> _messages;
        private static string _path = Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.Parent.FullName;

        [SetUp]
        public void Setup()
        {
            List<List<string>> allLines = new List<List<string>>();
            var files = Directory.GetFiles($"{_path}\\output_test");
            foreach (string file in files)
            {
                allLines.Add(new List<string>(File.ReadAllLines(file)));
            }

            _messages = new List<List<string>>();
            var firstLine = GetFirstCommonLine(allLines);
            var lastLine = GetLastCommonLine(allLines);
            foreach (var lines in allLines)
            {
                var firstIndex = lines.IndexOf(firstLine);
                var lastIndex = lines.IndexOf(lastLine);
                _messages.Add(lines.GetRange(firstIndex, lastIndex - firstIndex + 1));
            }
        }

        [TearDown]
        public void TearDown()
        {
            _messages.Clear();
        }

        [Test]
        public void message_collections_received_by_clients_should_be_equal()
        { 
            foreach (var lines in _messages)
            {
                foreach (var toCompare in _messages)
                {
                    Assert.IsTrue(lines.SequenceEqual(toCompare));
                }
            }            
        }

        private string GetFirstCommonLine(List<List<string>> allLines)
        {
            foreach (var lines in allLines)
            {
                int count = 0;
                foreach (var toFind in allLines)
                {
                    if (toFind.IndexOf(lines[0]) != -1)
                    {
                        count++;
                    }
                }

                if (count == allLines.Count)
                {
                    return lines[0];
                }
            }

            return null;
        }

        private string GetLastCommonLine(List<List<string>> allLines)
        {
            foreach (var lines in allLines)
            {
                int count = 0;
                foreach (var toFind in allLines)
                {
                    if (toFind.LastIndexOf(lines[lines.Count - 1]) != -1)
                    {
                        count++;
                    }
                }

                if (count == allLines.Count)
                {
                    return lines[lines.Count - 1];
                }
            }

            return null;
        }
    }
}