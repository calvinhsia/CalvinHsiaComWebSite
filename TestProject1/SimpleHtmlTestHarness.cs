using System.Diagnostics;

namespace TestProject1
{
    /// <summary>
    /// Simple test harness that creates an HTML file you can open in any browser
    /// This allows you to experiment with HTML/CSS/JS without needing Playwright
    /// </summary>
    [TestClass]
    public class SimpleHtmlTestHarness
    {
        /// <summary>
        /// Creates a standalone HTML file that loads your Blazor WASM app
        /// You can modify this file to experiment with HTML/CSS/JS
        /// </summary>
        [TestMethod]
        public void CreateInteractiveHtmlTestHarness()
        {
            var htmlContent = @"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""utf-8"" />
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no"" />
    <title>Blazor WASM Test Harness</title>
    <base href=""/"" />
    
    <!-- Link to your actual CSS files -->
    <link href=""http://localhost:5000/css/app.css"" rel=""stylesheet"" />
    <link href=""http://localhost:5000/css/wordscape-game.css"" rel=""stylesheet"" />
    <link href=""http://localhost:5000/css/wordament-game.css"" rel=""stylesheet"" />
    
    <style>
        /* Add your experimental CSS here */
        body {
            font-family: Arial, sans-serif;
            margin: 0;
            padding: 20px;
            background: #f0f0f0;
        }
        
        .test-harness-container {
            max-width: 1200px;
            margin: 0 auto;
            background: white;
            padding: 20px;
            border-radius: 8px;
            box-shadow: 0 2px 10px rgba(0,0,0,0.1);
        }
        
        .experiment-area {
            border: 2px dashed #ccc;
            padding: 20px;
            margin: 20px 0;
            background: #fafafa;
        }
        
        .controls {
            margin: 20px 0;
            padding: 15px;
            background: #e3f2fd;
            border-radius: 4px;
        }
        
        .controls button {
            margin: 5px;
            padding: 10px 20px;
            background: #2196F3;
            color: white;
            border: none;
            border-radius: 4px;
            cursor: pointer;
        }
        
        .controls button:hover {
            background: #1976D2;
        }
        
        #output {
            margin-top: 20px;
            padding: 15px;
            background: #fff;
            border: 1px solid #ddd;
            border-radius: 4px;
            min-height: 100px;
            font-family: 'Courier New', monospace;
        }
        
        /* Example grid for testing */
        .test-grid {
            display: grid;
            grid-template-columns: repeat(4, 80px);
            gap: 10px;
            margin: 20px 0;
        }
        
        .test-cell {
            width: 80px;
            height: 80px;
            background: #4CAF50;
            color: white;
            display: flex;
            align-items: center;
            justify-content: center;
            font-size: 24px;
            font-weight: bold;
            border-radius: 8px;
            cursor: pointer;
            transition: all 0.3s ease;
        }
        
        .test-cell:hover {
            transform: scale(1.1);
            background: #45a049;
            box-shadow: 0 4px 8px rgba(0,0,0,0.2);
        }
        
        .test-cell.selected {
            background: #ff9800;
            transform: scale(1.05);
        }
    </style>
</head>
<body>
    <div class=""test-harness-container"">
        <h1>?? Blazor WASM Interactive Test Harness</h1>
        <p>Use this page to experiment with HTML, CSS, and JavaScript for your Blazor WASM app.</p>
        
        <div class=""controls"">
            <h3>Controls</h3>
            <button onclick=""testFunction1()"">Test Function 1</button>
            <button onclick=""testFunction2()"">Test Function 2</button>
            <button onclick=""clearOutput()"">Clear Output</button>
            <button onclick=""testGridInteraction()"">Test Grid Interaction</button>
        </div>
        
        <div class=""experiment-area"">
            <h3>Experiment Area - Modify this section!</h3>
            <p>Add your HTML elements here to test layouts, styling, and interactions.</p>
            
            <!-- Example: Test grid similar to your games -->
            <div class=""test-grid"" id=""testGrid"">
                <div class=""test-cell"" data-letter=""A"" onclick=""selectCell(this)"">A</div>
                <div class=""test-cell"" data-letter=""B"" onclick=""selectCell(this)"">B</div>
                <div class=""test-cell"" data-letter=""C"" onclick=""selectCell(this)"">C</div>
                <div class=""test-cell"" data-letter=""D"" onclick=""selectCell(this)"">D</div>
                <div class=""test-cell"" data-letter=""E"" onclick=""selectCell(this)"">E</div>
                <div class=""test-cell"" data-letter=""F"" onclick=""selectCell(this)"">F</div>
                <div class=""test-cell"" data-letter=""G"" onclick=""selectCell(this)"">G</div>
                <div class=""test-cell"" data-letter=""H"" onclick=""selectCell(this)"">H</div>
                <div class=""test-cell"" data-letter=""I"" onclick=""selectCell(this)"">I</div>
                <div class=""test-cell"" data-letter=""J"" onclick=""selectCell(this)"">J</div>
                <div class=""test-cell"" data-letter=""K"" onclick=""selectCell(this)"">K</div>
                <div class=""test-cell"" data-letter=""L"" onclick=""selectCell(this)"">L</div>
            </div>
            
            <div id=""selectedWord"" style=""font-size: 24px; font-weight: bold; margin-top: 10px;"">
                Selected: <span id=""wordDisplay""></span>
            </div>
        </div>
        
        <div id=""output"">
            <strong>Output Console:</strong><br>
            <div id=""outputContent""></div>
        </div>
    </div>

    <script>
        // Your experimental JavaScript goes here
        
        let selectedCells = [];
        
        function log(message) {
            const output = document.getElementById('outputContent');
            const timestamp = new Date().toLocaleTimeString();
            output.innerHTML += `[${timestamp}] ${message}<br>`;
            output.scrollTop = output.scrollHeight;
        }
        
        function clearOutput() {
            document.getElementById('outputContent').innerHTML = '';
            log('Output cleared');
        }
        
        function testFunction1() {
            log('Test Function 1 executed!');
            log('You can modify this function to test anything you want.');
        }
        
        function testFunction2() {
            log('Test Function 2 executed!');
            
            // Example: Test fetch to your API
            log('Testing fetch (if server is running)...');
            fetch('http://localhost:5000/')
                .then(response => {
                    log(`Fetch successful! Status: ${response.status}`);
                    return response.text();
                })
                .then(data => {
                    log(`Received ${data.length} characters`);
                })
                .catch(error => {
                    log(`Fetch error: ${error.message}`);
                });
        }
        
        function selectCell(cell) {
            const letter = cell.dataset.letter;
            
            if (cell.classList.contains('selected')) {
                // Deselect
                cell.classList.remove('selected');
                selectedCells = selectedCells.filter(c => c !== cell);
                log(`Deselected: ${letter}`);
            } else {
                // Select
                cell.classList.add('selected');
                selectedCells.push(cell);
                log(`Selected: ${letter}`);
            }
            
            updateWordDisplay();
        }
        
        function updateWordDisplay() {
            const word = selectedCells.map(c => c.dataset.letter).join('');
            document.getElementById('wordDisplay').textContent = word || '(none)';
        }
        
        function testGridInteraction() {
            log('Testing grid interaction...');
            
            // Simulate selecting cells
            const cells = document.querySelectorAll('.test-cell');
            selectedCells = [];
            cells.forEach(cell => cell.classList.remove('selected'));
            
            // Select first 4 cells to spell a word
            for (let i = 0; i < 4 && i < cells.length; i++) {
                setTimeout(() => {
                    selectCell(cells[i]);
                }, i * 300);
            }
        }
        
        // Initialize
        log('Test harness loaded successfully!');
        log('Modify the HTML, CSS, and JavaScript in this file to experiment.');
        log('');
        log('Tips:');
        log('- Edit the CSS in the <style> section to test different styles');
        log('- Edit the HTML in the experiment-area to test layouts');
        log('- Add your own JavaScript functions to test interactions');
        log('- Use browser DevTools (F12) to debug and inspect');
    </script>
</body>
</html>";

            var outputPath = Path.Combine(
                Path.GetDirectoryName(typeof(SimpleHtmlTestHarness).Assembly.Location)!, 
                "interactive-test-harness.html"
            );
            
            File.WriteAllText(outputPath, htmlContent);
            
            Console.WriteLine($"Interactive test harness created at: {outputPath}");
            Console.WriteLine();
            Console.WriteLine("To use this test harness:");
            Console.WriteLine("1. Make sure your Blazor WASM app is running (dotnet run in Client folder)");
            Console.WriteLine("2. Open the generated HTML file in your browser");
            Console.WriteLine("3. Edit the HTML file to experiment with different HTML/CSS/JS");
            Console.WriteLine("4. Refresh the browser to see your changes");
            Console.WriteLine();
            
            // Try to open the file in the default browser
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = outputPath,
                    UseShellExecute = true
                });
                Console.WriteLine("Opening test harness in your default browser...");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not auto-open browser: {ex.Message}");
                Console.WriteLine($"Please manually open: {outputPath}");
            }
            
            Assert.IsTrue(File.Exists(outputPath), "Test harness file was not created");
        }

        /// <summary>
        /// Creates a test harness that embeds your actual Blazor app in an iframe
        /// This lets you interact with the real app while experimenting with wrapper HTML/CSS/JS
        /// </summary>
        [TestMethod]
        public void CreateIframeTestHarness()
        {
            var htmlContent = @"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""utf-8"" />
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
    <title>Blazor App - Iframe Test Harness</title>
    
    <style>
        body {
            font-family: Arial, sans-serif;
            margin: 0;
            padding: 0;
            display: flex;
            height: 100vh;
        }
        
        .sidebar {
            width: 300px;
            background: #263238;
            color: white;
            padding: 20px;
            overflow-y: auto;
        }
        
        .sidebar h2 {
            margin-top: 0;
            color: #4CAF50;
        }
        
        .sidebar button {
            width: 100%;
            padding: 10px;
            margin: 5px 0;
            background: #4CAF50;
            color: white;
            border: none;
            border-radius: 4px;
            cursor: pointer;
            font-size: 14px;
        }
        
        .sidebar button:hover {
            background: #45a049;
        }
        
        .sidebar input, .sidebar select {
            width: 100%;
            padding: 8px;
            margin: 5px 0;
            border-radius: 4px;
            border: 1px solid #ccc;
        }
        
        .sidebar label {
            display: block;
            margin-top: 10px;
            font-weight: bold;
        }
        
        .main-content {
            flex: 1;
            display: flex;
            flex-direction: column;
        }
        
        .controls-bar {
            background: #f0f0f0;
            padding: 10px;
            border-bottom: 1px solid #ccc;
        }
        
        .controls-bar button {
            margin: 0 5px;
            padding: 8px 16px;
            background: #2196F3;
            color: white;
            border: none;
            border-radius: 4px;
            cursor: pointer;
        }
        
        .controls-bar button:hover {
            background: #1976D2;
        }
        
        #blazorFrame {
            flex: 1;
            border: none;
            background: white;
        }
        
        .log-section {
            height: 150px;
            background: #1e1e1e;
            color: #d4d4d4;
            font-family: 'Courier New', monospace;
            font-size: 12px;
            padding: 10px;
            overflow-y: auto;
            border-top: 2px solid #4CAF50;
        }
        
        .log-entry {
            padding: 2px 0;
        }
        
        .log-entry.info { color: #4CAF50; }
        .log-entry.warning { color: #FFA500; }
        .log-entry.error { color: #f44336; }
    </style>
</head>
<body>
    <div class=""sidebar"">
        <h2>?? Test Controls</h2>
        
        <label>Navigate to:</label>
        <select id=""pageSelector"" onchange=""navigateToPage()"">
            <option value=""/"">Home</option>
            <option value=""/wordscape"">WordScape Game</option>
            <option value=""/wordament"">Wordament Game</option>
            <option value=""/logo"">Logo Game</option>
        </select>
        
        <label>Server URL:</label>
        <input type=""text"" id=""serverUrl"" value=""http://localhost:5000"" />
        
        <button onclick=""reloadFrame()"">?? Reload Frame</button>
        <button onclick=""openInNewTab()"">?? Open in New Tab</button>
        <button onclick=""clearLogs()"">??? Clear Logs</button>
        
        <hr style=""margin: 20px 0; border-color: #444;"">
        
        <h3>Inject Custom CSS</h3>
        <textarea id=""customCss"" rows=""6"" style=""width: 100%; font-family: monospace; font-size: 12px;"">
/* Add your experimental CSS here */
.test-highlight {
    background: yellow !important;
}
        </textarea>
        <button onclick=""injectCustomCss()"">?? Inject CSS</button>
        
        <hr style=""margin: 20px 0; border-color: #444;"">
        
        <h3>Inject Custom JS</h3>
        <textarea id=""customJs"" rows=""6"" style=""width: 100%; font-family: monospace; font-size: 12px;"">
// Add your experimental JavaScript here
console.log('Custom JS injected!');
        </textarea>
        <button onclick=""injectCustomJs()"">?? Inject JS</button>
        
        <hr style=""margin: 20px 0; border-color: #444;"">
        
        <h3>Quick Actions</h3>
        <button onclick=""inspectElement()"">?? Inspect Element</button>
        <button onclick=""takeScreenshot()"">?? Screenshot Info</button>
        <button onclick=""getPageInfo()"">?? Page Info</button>
    </div>
    
    <div class=""main-content"">
        <div class=""controls-bar"">
            <button onclick=""reloadFrame()"">Reload</button>
            <button onclick=""window.history.back()"">? Back</button>
            <button onclick=""window.history.forward()"">Forward ?</button>
            <span id=""currentUrl"" style=""margin-left: 20px; color: #666;""></span>
        </div>
        
        <iframe id=""blazorFrame"" src=""http://localhost:5000""></iframe>
        
        <div class=""log-section"" id=""logSection"">
            <div class=""log-entry info"">Test harness initialized. Start your Blazor WASM app with: dotnet run</div>
        </div>
    </div>

    <script>
        const frame = document.getElementById('blazorFrame');
        const serverUrlInput = document.getElementById('serverUrl');
        const pageSelector = document.getElementById('pageSelector');
        
        function log(message, type = 'info') {
            const logSection = document.getElementById('logSection');
            const timestamp = new Date().toLocaleTimeString();
            const entry = document.createElement('div');
            entry.className = `log-entry ${type}`;
            entry.textContent = `[${timestamp}] ${message}`;
            logSection.appendChild(entry);
            logSection.scrollTop = logSection.scrollHeight;
        }
        
        function clearLogs() {
            document.getElementById('logSection').innerHTML = '';
            log('Logs cleared');
        }
        
        function navigateToPage() {
            const baseUrl = serverUrlInput.value;
            const page = pageSelector.value;
            const url = baseUrl + page;
            frame.src = url;
            log(`Navigating to: ${url}`);
            updateCurrentUrl(url);
        }
        
        function reloadFrame() {
            frame.src = frame.src;
            log('Frame reloaded');
        }
        
        function openInNewTab() {
            const url = frame.src;
            window.open(url, '_blank');
            log(`Opened in new tab: ${url}`);
        }
        
        function updateCurrentUrl(url) {
            document.getElementById('currentUrl').textContent = url || frame.src;
        }
        
        function injectCustomCss() {
            try {
                const css = document.getElementById('customCss').value;
                const frameDoc = frame.contentDocument || frame.contentWindow.document;
                
                // Remove previous custom style if exists
                const oldStyle = frameDoc.getElementById('customInjectedStyle');
                if (oldStyle) oldStyle.remove();
                
                // Add new style
                const style = frameDoc.createElement('style');
                style.id = 'customInjectedStyle';
                style.textContent = css;
                frameDoc.head.appendChild(style);
                
                log('Custom CSS injected successfully', 'info');
            } catch (error) {
                log(`Error injecting CSS: ${error.message}`, 'error');
                log('Note: Cross-origin restrictions may prevent injection if server is on different origin', 'warning');
            }
        }
        
        function injectCustomJs() {
            try {
                const js = document.getElementById('customJs').value;
                const frameWin = frame.contentWindow;
                frameWin.eval(js);
                log('Custom JavaScript executed successfully', 'info');
            } catch (error) {
                log(`Error executing JavaScript: ${error.message}`, 'error');
            }
        }
        
        function inspectElement() {
            log('Open browser DevTools (F12) and use the element picker', 'info');
            log('You can also right-click inside the frame and select ""Inspect""', 'info');
        }
        
        function takeScreenshot() {
            log('To take a screenshot:', 'info');
            log('1. Press F12 to open DevTools', 'info');
            log('2. Press Ctrl+Shift+P (or Cmd+Shift+P on Mac)', 'info');
            log('3. Type ""screenshot"" and select ""Capture full size screenshot""', 'info');
        }
        
        function getPageInfo() {
            try {
                const frameDoc = frame.contentDocument || frame.contentWindow.document;
                const title = frameDoc.title;
                const url = frame.src;
                log(`Page Title: ${title}`, 'info');
                log(`Page URL: ${url}`, 'info');
                log(`Document Ready State: ${frameDoc.readyState}`, 'info');
            } catch (error) {
                log(`Cannot access frame content: ${error.message}`, 'error');
            }
        }
        
        // Monitor frame loading
        frame.addEventListener('load', () => {
            log('Frame loaded successfully', 'info');
            updateCurrentUrl(frame.src);
        });
        
        frame.addEventListener('error', () => {
            log('Error loading frame', 'error');
            log('Make sure your Blazor WASM app is running', 'warning');
        });
        
        // Update current URL on page load
        updateCurrentUrl(frame.src);
        
        log('Ready! Select a page from the dropdown or modify the server URL');
        log('Tip: Use the DevTools (F12) to inspect the Blazor app inside the iframe');
    </script>
</body>
</html>";

            var outputPath = Path.Combine(
                Path.GetDirectoryName(typeof(SimpleHtmlTestHarness).Assembly.Location)!,
                "iframe-test-harness.html"
            );

            File.WriteAllText(outputPath, htmlContent);

            Console.WriteLine($"Iframe test harness created at: {outputPath}");
            Console.WriteLine();
            Console.WriteLine("This test harness provides:");
            Console.WriteLine("- Iframe embedding of your actual Blazor app");
            Console.WriteLine("- Live CSS and JavaScript injection");
            Console.WriteLine("- Navigation controls");
            Console.WriteLine("- Console logging");
            Console.WriteLine();
            Console.WriteLine("To use:");
            Console.WriteLine("1. Start your Blazor app: dotnet run --project Client/Client.csproj");
            Console.WriteLine("2. Open the generated HTML file in your browser");
            Console.WriteLine("3. Use the sidebar controls to experiment");
            Console.WriteLine();

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = outputPath,
                    UseShellExecute = true
                });
                Console.WriteLine("Opening iframe test harness in your default browser...");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not auto-open browser: {ex.Message}");
                Console.WriteLine($"Please manually open: {outputPath}");
            }

            Assert.IsTrue(File.Exists(outputPath), "Test harness file was not created");
        }
    }
}
