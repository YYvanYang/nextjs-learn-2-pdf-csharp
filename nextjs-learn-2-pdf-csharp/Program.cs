using iText.Kernel.Pdf;
using iText.Kernel.Utils;
using iText.Layout.Element;
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
            //    "--proxy-server=http://127.0.0.1:10809"  // replace with your proxy server and port
            //},
            Headless = true
        });
        var page = await browser.NewPageAsync();

        var firstPage = "https://help.obsidian.md/Home";

        // Navigate to the initial URL
        await page.GoToAsync(firstPage);

        var allLinks = await getAllLinks(page);

        //// Get all links
        //var links = await page.EvaluateExpressionAsync<string[]>(
        //    @"Array.from(document.querySelectorAll(""[id$='-content-nav'] a"")).map(a => a.href)");
        allLinks.Insert(1, firstPage);

        var links = allLinks.ToArray();

        // Loop through each link, navigate to the page, and save the article content to a PDF
        for (int i = 0; i < links.Length; i++)
        {
            try
            {
                await scrapyPage(pdfFolderPath, page, links, i);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                if (i > 0)
                {
                    i--;
                }
            }

        }


        // get pdfFiles
        var pdfFiles = Directory.GetFiles(pdfFolderPath);
        var finalFile = Path.Combine(pdfFolderPath, "obsidian.pdf");
        MergePdfFiles(pdfFiles, finalFile);

        // Close the browser
        await browser.CloseAsync();
    }

    private static async Task scrapyPage(string pdfFolderPath, IPage page, string[] links, int i)
    {
        var url = links[i];
        Console.WriteLine(url.ToString());
        await page.GoToAsync(url);

        // Get the initial scroll height of the page
        //var previousScrollHeight = await page.EvaluateExpressionAsync<int>("document.body.scrollHeight");
        var totalHeight = 0;
        var distance = 50;
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



        //// Now get the outerHTML of the article element
        //var articleOuterHtml = await page.EvaluateFunctionAsync<string>("element => element.outerHTML", await page.QuerySelectorAsync("article"));

        await Task.Delay(500);

        if (i == 0)
        {
            try
            {
                // Get article content
                var content = await page.EvaluateExpressionAsync<string>(
                    "document.querySelector('.markdown-preview-section').outerHTML");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                // Get article content
                var content = await page.EvaluateExpressionAsync<string>(
                    "document.body.outerHTML");
            }

        }
        else
        {
            try
            {
                // Get article content
                var content = await page.EvaluateExpressionAsync<string>(
                    "document.querySelector('.markdown-preview-section').outerHTML");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                i--;
            }
        }


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

        // 目录树页
        var directoryHtml = new StringBuilder("<html><body><ul>");


        foreach (var item in treeItems)
        {
            var innerItem = await item.QuerySelectorAsync(".tree-item-inner");
            // 父节点名称
            var parentNodeName = await innerItem.EvaluateFunctionAsync<string>("element => element.textContent");

            directoryHtml.Append($"<li style='font-weight:700'>{parentNodeName}</li>");

            // 点击父节点以展开子节点
            await item.ClickAsync();

            // 等待动画完成和子节点加载
            await page.WaitForTimeoutAsync(500); // 可以调整这个等待时间，确保子节点加载完成

            // 获取展开后的子节点链接
            var childLinks = await item.QuerySelectorAllAsync("a");
            foreach (var childLink in childLinks)
            {
                // 获取并存储链接地址
                var link = await (await childLink.GetPropertyAsync("href")).JsonValueAsync<string>();
                allLinks.Add(link);

                var linkInnerItem = await childLink.QuerySelectorAsync(".tree-item-inner");

                var nodeName = await linkInnerItem.EvaluateFunctionAsync<string>("element => element.textContent");

                directoryHtml.Append($"<li><a href='{link}'>{nodeName}</a></li>");
            }

            // 检查当前节点是否仍然是 .is-collapsed
            var isCollapsed = await item.EvaluateFunctionAsync<bool>("e => e.classList.contains('is-collapsed')");
            if (isCollapsed)
            {
                // 如果当前节点没有子节点，将其自己的链接添加到列表中
                var ownLink = await (await item.GetPropertyAsync("href")).JsonValueAsync<string>();
                allLinks.Add(ownLink);
            }
        }

        directoryHtml.Append("</ul></body></html>");

        // 保存目录页到文件
        var filePath = "directory.html";
        await System.IO.File.WriteAllTextAsync(filePath, directoryHtml.ToString());

        Console.WriteLine($"Directory page saved to {filePath}");

        var directoryPage = Path.Combine(Environment.CurrentDirectory, filePath);
        allLinks.Insert(0, directoryPage);

        // 所有链接都已收集到allLinks列表中
        Console.WriteLine("Collected Links:");
        foreach (var link in allLinks)
        {
            Console.WriteLine(link);
        }

        return allLinks;
    }

}
