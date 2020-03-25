using System;
using System.IO;
using Atol.Api.Common;
using Atol.Api.Misc;
using Moq;
using NUnit.Framework;

namespace Atol.Api.Tests.Integration
{
    /// <summary>
    /// Тест на загрузку кривых пакетов.
    /// </summary>
    [TestFixture]
    public class BadPackageTests : TestBase
    {
        [Test]
        [TestCase("file_with_dubles.chrome.txt")]
        public void ParseCsv_FileWithDubles_ReturnFailure(string packFile)
        {
            Check("Тест на загрузку пака с дублями. Даем файл с дублями, ожидаем Fail.", () =>
            {
                var parser = new DataParser(new HashManager(new InMemoryFileSystem()));
                using(var stream = File.OpenRead($@"Tests\\Data\\{packFile}"))
                {
                    var result = parser.ParseCsv(stream);
                    Assert.True(result.IsFailure);
                    Console.WriteLine(result.Error);
                }
            });
        }

        [Test]
        [TestCase("bad_lines_chrome.txt")]
        public void ParseCsv_FileWithIncorrectLines_ReturnFailure(string packFile)
        {
            Check("Тест на загрузку пака с кривыми строчками. Ожидается Fail.", () =>
            {
                var parser = new DataParser(new HashManager(new InMemoryFileSystem()) );
                using(var stream = File.OpenRead($@"Tests\\Data\\{packFile}"))
                {
                    var result = parser.ParseCsv(stream);
                    Assert.True(result.IsFailure);
                    Console.WriteLine(result.Error);
                }
            });
        }

        [Test]
        [TestCase("empty_lines_chrome.txt")]
        public void ParseCsv_FileWithEmptyLinesInside_RetuensFailure(string packFile)
        {
            Check("Тест на загрузку пака, в котором встречаются пустые строки. Ожидаем Fail.", () =>
            {
                var parser = new DataParser(new HashManager(new InMemoryFileSystem()));
                using(var stream = File.OpenRead($@"Tests\\Data\\{packFile}"))
                {
                    var result = parser.ParseCsv(stream);
                    Assert.True(result.IsFailure);
                    Console.WriteLine(result.Error);
                }
            });
        }
    }
}