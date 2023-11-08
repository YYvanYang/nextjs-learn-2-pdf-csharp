using iText.Kernel.Pdf;
using iText.Kernel.Utils;
using PuppeteerSharp;
using PuppeteerSharp.Media;
using System.Text.RegularExpressions;

class Program
{
    public static async Task Main(string[] args)
    {
        // Create a folder for saving PDFs
        var pdfFolderPath = Path.Combine(Environment.CurrentDirectory, "PDFs");

        if (Directory.Exists(pdfFolderPath))
        {
            // clear the folder
            Directory.Delete(pdfFolderPath, true);
            Directory.CreateDirectory(pdfFolderPath);
        }
        else
        {
            Directory.CreateDirectory(pdfFolderPath);
        }
        // Launch browser
        var browser = await Puppeteer.LaunchAsync(new LaunchOptions
        {
            ExecutablePath = @"C:\Program Files\Google\Chrome\Application\chrome.exe",
            //Args = new string[]
            //{
            //    "--proxy-server=http://127.0.0.1:7890"  // replace with your proxy server and port
            //},
            Headless = true
        });
        var page = await browser.NewPageAsync();

        // Navigate to the initial URL
        await page.GoToAsync("https://nextjs.org/learn/dashboard-app");

        var elementHandle = await page.QuerySelectorAsync("[id$='-trigger-nav']");
        if (elementHandle != null)
        {
            var className = await page.EvaluateFunctionAsync<string>("element => element.className", elementHandle);
            Console.WriteLine($"Element found with class name: {className}");
        }
        else
        {
            Console.WriteLine("No element found with a class name ending with '-trigger-nav'");
        }

        // Click the button to reveal the nav tree
        await page.ClickAsync("[id$='-trigger-nav']");

        // Get all links
        var links = await page.EvaluateExpressionAsync<string[]>(
            @"Array.from(document.querySelectorAll(""[id$='-content-nav'] a"")).map(a => a.href + '|||' + a.firstElementChild.textContent)");

        // Loop through each link, navigate to the page, and save the article content to a PDF
        for (int i = 0; i < links.Length; i++)
        {
            var parts = links[i].Split("|||");
            var url = parts[0];
            var index = parts[1] == "" ? "0" : parts[1];
            Console.WriteLine(url.ToString());
            await page.GoToAsync(url);

            // Get the initial scroll height of the page
            var previousScrollHeight = await page.EvaluateExpressionAsync<int>("document.body.scrollHeight");
            var totalHeight = 0;
            var distance = 300;
            while (true)
            {
                // Scroll down by increasing the scrollTop property of the page's body
                //await page.EvaluateExpressionAsync("window.scrollTo(0, document.body.scrollHeight)");
                await page.EvaluateExpressionAsync($"window.scrollBy(0, {distance})");

                // Wait for a little bit for new content to load
                await Task.Delay(500);
                totalHeight += distance;

                // Check if the scroll height has increased, indicating new content has loaded
                var newScrollHeight = await page.EvaluateExpressionAsync<int>("document.body.scrollHeight");
                Console.WriteLine($"totalHeight: {totalHeight}");
                Console.WriteLine($"newScrollHeight: {newScrollHeight}");
                if (newScrollHeight < totalHeight)
                {
                    // No new content has loaded, break out of the loop
                    break;
                }


            }

            // Now all content should be loaded, proceed with other actions...
            // Set header and footer elements to null
            await page.EvaluateFunctionAsync(@"() => {
                var header = document.querySelector('header');
                var aside = document.querySelector('aside');
                var footer = document.querySelector('footer');
                var cconsentBar = document.querySelector('#cconsent-bar');
                var cconsentModal = document.querySelector('#cconsent-modal');
                var feedback = document.querySelector(""[class^='feedback_inlineWrapper']"");
                if (header) {
                    header.parentNode.removeChild(header);
                }
                if (aside) {
                    aside.parentNode.removeChild(aside);
                }
                if (footer) {
                    footer.parentNode.removeChild(footer);
                }
                if (cconsentBar) {
                    cconsentBar.parentNode.removeChild(cconsentBar);
                }
                if (cconsentModal) {
                    cconsentModal.parentNode.removeChild(cconsentModal);
                }
                if (feedback) {
                    feedback.parentNode.removeChild(feedback);
                }

            }");


            //// Now get the outerHTML of the article element
            //var articleOuterHtml = await page.EvaluateFunctionAsync<string>("element => element.outerHTML", await page.QuerySelectorAsync("article"));

            // Get article content
            var content = await page.EvaluateExpressionAsync<string>(
                "document.querySelector('article').outerHTML");


            // Set up PDF options
            var pdfOptions = new PdfOptions
            {
                Format = PaperFormat.A4,
                MarginOptions = new MarginOptions
                {
                    Top = "2cm",
                    Right = "1cm",
                    Bottom = "2cm",
                    Left = "1cm"
                },
                DisplayHeaderFooter = false,
                PrintBackground = true
            };



            // Save PDF to the created folder
            var pdfFilePath = Path.Combine(pdfFolderPath, $"Article_{index}.pdf");
            // Save content to PDF
            await page.PdfAsync(pdfFilePath, pdfOptions);

        }


        // get pdfFiles
        var pdfFiles = Directory.GetFiles(pdfFolderPath);
        var sortedPdfFiles = pdfFiles.OrderBy(f => GetFileNumber(f)).ToList();
        var finalFile = Path.Combine(pdfFolderPath, "nextjs-learn.pdf");
        MergePdfFiles(sortedPdfFiles, finalFile);

        // Close the browser
        await browser.CloseAsync();
    }

    static void saveAll(string pdfFolderPath)
    {
        // get pdfFiles
        var pdfFiles = Directory.GetFiles(pdfFolderPath);
        var sortedPdfFiles = pdfFiles.OrderBy(f => GetFileNumber(f)).ToList();
        // 打印排序后的文件名
        sortedPdfFiles.ForEach(f => Console.WriteLine(f));
        var finalFile = Path.Combine(pdfFolderPath, "nextjs-learn.pdf");
        MergePdfFiles(sortedPdfFiles, finalFile);
    }

    // 提取文件名中的数字并转换为整数
    static int GetFileNumber(string fileName)
    {
        // 正则表达式匹配所有数字
        var match = Regex.Match(Path.GetFileName(fileName), @"\d+");
        var num = match.Success ? int.Parse(match.Value) : 0;
        Console.WriteLine(num);
        return num;
    }

    static void MergePdfFiles(IEnumerable<string> inputPdfFiles, string outputPdfFile)
    {
        using (var writer = new PdfWriter(outputPdfFile))
        {
            using (var pdf = new PdfDocument(writer))
            {
                var pdfMerger = new PdfMerger(pdf);

                foreach (var inputFile in inputPdfFiles)
                {
                    using (var pdfToMerge = new PdfDocument(new PdfReader(inputFile)))
                    {
                        pdfMerger.Merge(pdfToMerge, 1, pdfToMerge.GetNumberOfPages());
                    }
                }
            }
        }
    }

}
