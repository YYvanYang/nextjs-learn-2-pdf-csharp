using iText.Kernel.Pdf;
using iText.Kernel.Utils;
using PuppeteerSharp;
using PuppeteerSharp.Media;
using System.Text;

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

        var firstPage = "https://publish.obsidian.md/help-zh/%E7%94%B1%E6%AD%A4%E5%BC%80%E5%A7%8B";

        // Navigate to the initial URL
        await page.GoToAsync(firstPage);

        var allLinks = await getAllLinks(page);

        //var elementHandle = await page.QuerySelectorAsync("[id$='-trigger-nav']");
        //if (elementHandle != null)
        //{
        //    var className = await page.EvaluateFunctionAsync<string>("element => element.className", elementHandle);
        //    Console.WriteLine($"Element found with class name: {className}");
        //}
        //else
        //{
        //    Console.WriteLine("No element found with a class name ending with '-trigger-nav'");
        //}

        //// Click the button to reveal the nav tree
        //await page.ClickAsync("[id$='-trigger-nav']");

        //// Get all links
        //var links = await page.EvaluateExpressionAsync<string[]>(
        //    @"Array.from(document.querySelectorAll(""[id$='-content-nav'] a"")).map(a => a.href)");
        allLinks.Insert(0, firstPage);

        var links = allLinks.ToArray();

        // Loop through each link, navigate to the page, and save the article content to a PDF
        for (int i = 0; i < links.Length; i++)
        {
            var url = links[i];
            Console.WriteLine(url.ToString());
            await page.GoToAsync(url);

            // Get the initial scroll height of the page
            //var previousScrollHeight = await page.EvaluateExpressionAsync<int>("document.body.scrollHeight");
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
            //await page.EvaluateFunctionAsync(@"() => {
            //    var header = document.querySelector('header');
            //    var aside = document.querySelector('aside');
            //    var footer = document.querySelector('footer');
            //    var cconsentBar = document.querySelector('#cconsent-bar');
            //    var cconsentModal = document.querySelector('#cconsent-modal');
            //    var feedback = document.querySelector(""[class^='feedback_inlineWrapper']"");
            //    if (header) {
            //        header.parentNode.removeChild(header);
            //    }
            //    if (aside) {
            //        aside.parentNode.removeChild(aside);
            //    }
            //    if (footer) {
            //        footer.parentNode.removeChild(footer);
            //    }
            //    if (cconsentBar) {
            //        cconsentBar.parentNode.removeChild(cconsentBar);
            //    }
            //    if (cconsentModal) {
            //        cconsentModal.parentNode.removeChild(cconsentModal);
            //    }
            //    if (feedback) {
            //        feedback.parentNode.removeChild(feedback);
            //    }

            //}");


            //// Now get the outerHTML of the article element
            //var articleOuterHtml = await page.EvaluateFunctionAsync<string>("element => element.outerHTML", await page.QuerySelectorAsync("article"));

            await Task.Delay(500);

            // Get article content
            var content = await page.EvaluateExpressionAsync<string>(
                "document.querySelector('.markdown-preview-section').outerHTML");

            //await page.EvaluateFunctionAsync("content => { document.body.innerHTML = content; }", content);



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
            var pdfFilePath = Path.Combine(pdfFolderPath, $"Article_{i}.pdf");
            // Save content to PDF
            await page.PdfAsync(pdfFilePath, pdfOptions);

        }


        // get pdfFiles
        var pdfFiles = Directory.GetFiles(pdfFolderPath);
        var finalFile = Path.Combine(pdfFolderPath, "nextjs-learn.pdf");
        MergePdfFiles(pdfFiles, finalFile);

        // Close the browser
        await browser.CloseAsync();
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

    static async Task<List<string>> getAllLinks(IPage page)
    {
        // 收集所有链接的列表
        var allLinks = new List<string>();

        // 查找所有父节点
        var treeItems = await page.QuerySelectorAllAsync(".nav-view-outer .tree-item.is-collapsed");

        foreach (var item in treeItems)
        {
            // 点击父节点以展开子节点
            await item.ClickAsync();

            // 等待动画完成和子节点加载
            await page.WaitForTimeoutAsync(500); // 可以调整这个等待时间，确保子节点加载完成

            // 获取展开后的子节点链接
            var childLinks = await item.QuerySelectorAllAsync("a");
            foreach (var childLink in childLinks)
            {
                // 获取并存储链接地址
                var link = await(await childLink.GetPropertyAsync("href")).JsonValueAsync<string>();
                allLinks.Add(link);
            }

            // 检查当前节点是否仍然是 .is-collapsed
            var isCollapsed = await item.EvaluateFunctionAsync<bool>("e => e.classList.contains('is-collapsed')");
            if (isCollapsed)
            {
                // 如果当前节点没有子节点，将其自己的链接添加到列表中
                var ownLink = await(await item.GetPropertyAsync("href")).JsonValueAsync<string>();
                allLinks.Add(ownLink);
            }
        }

        // 所有链接都已收集到allLinks列表中
        Console.WriteLine("Collected Links:");
        foreach (var link in allLinks)
        {
            Console.WriteLine(link);
        }

        return allLinks;
    }

}
