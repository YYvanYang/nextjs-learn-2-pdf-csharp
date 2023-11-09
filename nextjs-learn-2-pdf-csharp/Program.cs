using iText.Kernel.Pdf;
using iText.Kernel.Utils;
using PuppeteerSharp;
using PuppeteerSharp.Media;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Text.Json;
using System;

class Program
{
    public static async Task Main(string[] args)
    {
        string pdfFolderPath = PrepareDirectory();

        List<NavLink> navLinks = await GetNavLinks();
        var activeLinkSiblings = await GetActiveLinkSiblings(navLinks);
        var pagesContentList = await GetPagesContent(activeLinkSiblings);
        // Define the path where you want to save the PDF
        string pdfPath = Path.Combine(pdfFolderPath, "openai-document.pdf");
        await SaveContentToPDF(pagesContentList, pdfPath);

        return;

        //if (sideNavSections != null)
        //{
        //    var className = await page.EvaluateFunctionAsync<string>("element => element.className", elementHandle);
        //    Console.WriteLine($"Element found with class name: {className}");
        //}
        //else
        //{
        //    Console.WriteLine("No element found with a class name ending with '-trigger-nav'");
        //}

        // Click the button to reveal the nav tree
        //await page.ClickAsync("[id$='-trigger-nav']");

        //// Get all links
        //var links = await page.EvaluateExpressionAsync<string[]>(
        //    @"Array.from(document.querySelectorAll(""[id$='-content-nav'] a"")).map(a => a.href + '|||' + a.firstElementChild.textContent)");

        //// Loop through each link, navigate to the page, and save the article content to a PDF
        //for (int i = 0; i < links.Length; i++)
        //{
        //    var parts = links[i].Split("|||");
        //    var url = parts[0];
        //    var index = parts[1] == "" ? "0" : parts[1];
        //    Console.WriteLine(url.ToString());
        //    await page.GoToAsync(url);

        //    // Get the initial scroll height of the page
        //    var previousScrollHeight = await page.EvaluateExpressionAsync<int>("document.body.scrollHeight");
        //    var totalHeight = 0;
        //    var distance = 300;
        //    while (true)
        //    {
        //        // Scroll down by increasing the scrollTop property of the page's body
        //        //await page.EvaluateExpressionAsync("window.scrollTo(0, document.body.scrollHeight)");
        //        await page.EvaluateExpressionAsync($"window.scrollBy(0, {distance})");

        //        // Wait for a little bit for new content to load
        //        await Task.Delay(500);
        //        totalHeight += distance;

        //        // Check if the scroll height has increased, indicating new content has loaded
        //        var newScrollHeight = await page.EvaluateExpressionAsync<int>("document.body.scrollHeight");
        //        Console.WriteLine($"totalHeight: {totalHeight}");
        //        Console.WriteLine($"newScrollHeight: {newScrollHeight}");
        //        if (newScrollHeight < totalHeight)
        //        {
        //            // No new content has loaded, break out of the loop
        //            break;
        //        }


        //    }

        //    // Now all content should be loaded, proceed with other actions...
        //    // Set header and footer elements to null
        //    await page.EvaluateFunctionAsync(@"() => {
        //        var header = document.querySelector('header');
        //        var aside = document.querySelector('aside');
        //        var footer = document.querySelector('footer');
        //        var cconsentBar = document.querySelector('#cconsent-bar');
        //        var cconsentModal = document.querySelector('#cconsent-modal');
        //        var feedback = document.querySelector(""[class^='feedback_inlineWrapper']"");
        //        if (header) {
        //            header.parentNode.removeChild(header);
        //        }
        //        if (aside) {
        //            aside.parentNode.removeChild(aside);
        //        }
        //        if (footer) {
        //            footer.parentNode.removeChild(footer);
        //        }
        //        if (cconsentBar) {
        //            cconsentBar.parentNode.removeChild(cconsentBar);
        //        }
        //        if (cconsentModal) {
        //            cconsentModal.parentNode.removeChild(cconsentModal);
        //        }
        //        if (feedback) {
        //            feedback.parentNode.removeChild(feedback);
        //        }

        //    }");


        //    //// Now get the outerHTML of the article element
        //    //var articleOuterHtml = await page.EvaluateFunctionAsync<string>("element => element.outerHTML", await page.QuerySelectorAsync("article"));

        //    // Get article content
        //    var content = await page.EvaluateExpressionAsync<string>(
        //        "document.querySelector('article').outerHTML");


        //    // Set up PDF options
        //    var pdfOptions = new PdfOptions
        //    {
        //        Format = PaperFormat.A4,
        //        MarginOptions = new MarginOptions
        //        {
        //            Top = "2cm",
        //            Right = "1cm",
        //            Bottom = "2cm",
        //            Left = "1cm"
        //        },
        //        DisplayHeaderFooter = false,
        //        PrintBackground = true
        //    };



        //    // Save PDF to the created folder
        //    var pdfFilePath = Path.Combine(pdfFolderPath, $"Article_{index}.pdf");
        //    // Save content to PDF
        //    await page.PdfAsync(pdfFilePath, pdfOptions);

        //}


        //// get pdfFiles
        //var pdfFiles = Directory.GetFiles(pdfFolderPath);
        //var sortedPdfFiles = pdfFiles.OrderBy(f => GetFileNumber(f)).ToList();
        //var finalFile = Path.Combine(pdfFolderPath, "nextjs-learn.pdf");
        //MergePdfFiles(sortedPdfFiles, finalFile);

        //// Close the browser
        //await browser.CloseAsync();
    }

    public struct NavLink
    {
        public string Title;
        public List<(string Text, string Url)> Links;
    }

    public struct ActiveLinkSiblings
    {
        public string ActiveTitle;
        public List<(string Text, string Url)> SiblingLinks;
    }

    public struct PageContent
    {
        public string Title;
        public string Content;
    }

    public struct Link
    {
        public string Text;
        public string Url;
    }

    public static async Task LoadAllPageContent(IPage page)
    {
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
    }

    public static async Task<List<NavLink>> GetNavLinks()
    {
        // Launch browser
        var browser = await Puppeteer.LaunchAsync(new LaunchOptions
        {
            ExecutablePath = @"C:\Program Files\Google\Chrome\Application\chrome.exe",
            Headless = true
        });
        var page = await browser.NewPageAsync();

        await page.GoToAsync("https://platform.openai.com/docs/overview");

        var sideNavSections = await page.QuerySelectorAllAsync(".docs-nav .side-nav .side-nav-section");
        var navLinks = new List<NavLink>();

        foreach (var section in sideNavSections)
        {
            var header = await section.QuerySelectorAsync(".side-nav-header.subheading");
            var title = await (await header.GetPropertyAsync("textContent")).JsonValueAsync<string>();

            var links = await section.QuerySelectorAllAsync("a");
            var linkData = new List<(string Text, string Url)>();

            foreach (var link in links)
            {
                var text = await (await link.GetPropertyAsync("textContent")).JsonValueAsync<string>();
                var url = await (await link.GetPropertyAsync("href")).JsonValueAsync<string>();
                linkData.Add((text, url));
            }

            navLinks.Add(new NavLink
            {
                Title = title,
                Links = linkData
            });
        }

        await browser.CloseAsync();

        foreach (var navLink in navLinks)
        {
            Console.WriteLine(navLink.Title);
            foreach (var (Text, Url) in navLink.Links)
            {
                Console.WriteLine($"{Text} - {Url}");
            }
        }

        return navLinks;
    }

    public static async Task<List<ActiveLinkSiblings>> GetActiveLinkSiblings(List<NavLink> navLinks)
    {
        var browser = await Puppeteer.LaunchAsync(new LaunchOptions
        {
            ExecutablePath = @"C:\Program Files\Google\Chrome\Application\chrome.exe",
            Headless = true
        });
        var activeLinkSiblingsList = new List<ActiveLinkSiblings>();

        foreach (var navLink in navLinks)
        {
            foreach (var (Text, Url) in navLink.Links)
            {
                var page = await browser.NewPageAsync();
                await page.GoToAsync(Url);

                var activeLink = await page.QuerySelectorAsync(".scroll-link.side-nav-item.active.active-exact");
                if (activeLink != null)
                {
                    var activeTitle = await (await activeLink.GetPropertyAsync("textContent")).JsonValueAsync<string>();
                    var activeLinkHref = await (await activeLink.GetPropertyAsync("href")).JsonValueAsync<string>();
                    // here must declar Link[] type, else it will convert fail
                    var siblingLinksElements = await activeLink.EvaluateFunctionAsync<Link[]>(@"(activeLink) => {
                        const siblings = [];
                        let sibling = activeLink.nextElementSibling;
                        while (sibling) {
                            if (sibling.matches('.scroll-link.side-nav-item.side-nav-child')) {
                                let obj = { Text: sibling.textContent, Url: sibling.href };
                                siblings.push(obj);
                            }
                            sibling = sibling.nextElementSibling;
                        }
                        return siblings;
                    }", activeLink);


                    if (siblingLinksElements != null && siblingLinksElements.Length > 0)
                    {

                        var siblingLinks = siblingLinksElements.Select(link => (link.Text, link.Url)).ToList();
                        // add active link to the first position
                        siblingLinks.Insert(0, (activeTitle, activeLinkHref));
                        activeLinkSiblingsList.Add(new ActiveLinkSiblings
                        {
                            ActiveTitle = activeTitle,
                            SiblingLinks = siblingLinks
                        });

                    }



                }

                await page.CloseAsync();
            }
        }

        await browser.CloseAsync();

        foreach (var navLink in activeLinkSiblingsList)
        {
            Console.WriteLine(navLink.ActiveTitle);
            foreach (var (Text, Url) in navLink.SiblingLinks)
            {
                Console.WriteLine($"{Text} - {Url}");
            }
            Console.WriteLine();
            Console.WriteLine("===================");
            Console.WriteLine();
        }

        return activeLinkSiblingsList;
    }

    public static async Task<List<PageContent>> GetPagesContent(List<ActiveLinkSiblings> activeLinkSiblingsList)
    {
        var browser = await Puppeteer.LaunchAsync(new LaunchOptions
        {
            ExecutablePath = @"C:\Program Files\Google\Chrome\Application\chrome.exe",
            Headless = true
        });
        var pagesContentList = new List<PageContent>();

        foreach (var activeLinkSiblings in activeLinkSiblingsList)
        {
            foreach (var (Text, Url) in activeLinkSiblings.SiblingLinks)
            {
                var page = await browser.NewPageAsync();
                await page.GoToAsync(Url);
                Console.WriteLine($"fetching page: {Url}");
                await LoadAllPageContent(page);

                var contentElement = await page.QuerySelectorAsync(".docs-body");
                var content = "";
                if (contentElement != null)
                {
                    content = await (await contentElement.GetPropertyAsync("innerText")).JsonValueAsync<string>();
                }
                pagesContentList.Add(new PageContent
                {
                    Title = Text,
                    Content = content
                });
                await page.CloseAsync();
            }
        }

        await browser.CloseAsync();
        return pagesContentList;
    }

    public static async Task SaveContentToPDF(List<PageContent> pagesContentList, string filePath)
    {
        var browser = await Puppeteer.LaunchAsync(new LaunchOptions { ExecutablePath = @"C:\Program Files\Google\Chrome\Application\chrome.exe", Headless = true });
        var page = await browser.NewPageAsync();


        foreach (var pageContent in pagesContentList)
        {
            await page.SetContentAsync("<html><body></body></html>"); // Set an initial empty HTML content
            // For each content, create a new section in the HTML
            string sectionHtml = $"<section><h1>{pageContent.Title}</h1><div>{pageContent.Content}</div></section>";
            await page.EvaluateFunctionAsync(@"(html) => {
            document.body.insertAdjacentHTML('beforeend', html);
        }", sectionHtml);

        }

        // Adjust PDF options as necessary
        var pdfOptions = new PdfOptions
        {
            Format = PaperFormat.A4,
            PrintBackground = true,
            MarginOptions = { Top = "20px", Right = "20px", Bottom = "20px", Left = "20px" }
        };


        Console.WriteLine($"Saving PDF Document, path: {filePath}");
        await page.PdfAsync(filePath, pdfOptions);

        await browser.CloseAsync();
    }

    private static string PrepareDirectory()
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

        return pdfFolderPath;
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
