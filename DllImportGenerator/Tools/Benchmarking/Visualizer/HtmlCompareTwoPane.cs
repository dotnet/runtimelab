using System;
using System.Collections.Generic;
using System.Text;
using System.Web;

namespace Benchmarking.Visualizer
{
    internal class HtmlCompareTwoPane
    {
        public string Title { get; init; }
        public string SelectionTitle { get; init; }
        public string Pane1Title { get; init; }
        public string Pane2Title { get; init; }

        private const string DefaultStyle =
@"
            * { box-sizing: border-box; }
            html {
                overflow: hidden;
            }
            .row {
                display: flex;
            }
            .column {
                float: left;
                padding: 10px;
            }
            .left {
                width: 20%;
                height: 100vh;
            }
            .middle, .right {
                width: 40%;
                height: 100vh;
                border: black;
                border-width: 1px;
            }
            .constrainedDiv {
                height: 80%;
                overflow: auto;
            }
            .optionNameDiv {
                white-space: nowrap;
                overflow: hidden;
                text-overflow: ellipsis;
            }
            .comparePane {
                width: 100%;
                font-family: 'Courier New', Courier, monospace;
                font-size: medium;
                white-space: pre;
                resize: none;
            }
            .paneTgtDiv button {
                margin: 1pt;
            }
            #filterInput {
                background-position: 10px 12px;
                background-repeat: no-repeat;
                width: 100%;
                font-size: 16px;
                padding: 12px 20px 12px 5px;
                border: 1px solid #ddd;
                margin-bottom: 12px;
            }
            #allOptions {
                list-style-type: none;
                padding: 0;
                margin: 0;
            }
            #allOptions li > div {
                border: 1px solid #ddd;
                margin-top: 1px;
                background-color: #f6f6f6;
                padding: 12px;
                text-decoration: none;
                font-size: 18px;
                color: black;
            }
            #allOptions li div:hover:not(.header) {
                background-color: #eee;
            }
";

        public string Generate(IEnumerable<(string Name, string Content)> options)
        {
            var optionEntries = new StringBuilder();
            var jsonData = new StringBuilder();

            int i = 0;
            foreach (var opt in options)
            {
                optionEntries.AppendLine(
@$"<li><div>
    <div id=""optionName"" class=""optionNameDiv"">{opt.Name}</div>
    <div class=""paneTgtDiv""> <button onclick=""setPane1({i})"">1</button><button onclick=""setPane2({i})"">2</button></div></div>
</li>");
                // Escape the input twice since it is being embedded in a string as a JSON object.
                jsonData.AppendLine($"\"{{ \\\"content\\\": \\\"{HttpUtility.JavaScriptStringEncode(HttpUtility.JavaScriptStringEncode(opt.Content))}\\\" }}\",");

                ++i;
            }

            return
@$"
<!DOCTYPE html>
<html>
    <head>
        <title>{this.Title}</title>

        <!-- CSS for page -->
        <style>
            {DefaultStyle}
        </style>

        <!-- Script utilities -->
        <script>
            // Filter callback
            function filterInputCallback() {{
                var input, filter, ul, li, el, i, txtValue;
                input = document.getElementById(""filterInput"");
                filter = input.value.toUpperCase();
                ul = document.getElementById(""allOptions"");
                li = ul.getElementsByTagName(""li"");
                for (i = 0; i < li.length; i++) {{
                    el = li[i].querySelector(""#optionName"");
                    txtValue = el.textContent || el.innerText;
                    if (txtValue.toUpperCase().indexOf(filter) > -1) {{
                        li[i].style.display = """";
                    }} else {{
                        li[i].style.display = ""none"";
                    }}
                }}
            }}
            // Functions to set the panes
            function setPane1(dataId) {{
                var p1 = document.getElementById(""pane1"");
                setPane(p1, dataId);
            }}
            function setPane2(dataId) {{
                var p2 = document.getElementById(""pane2"");
                setPane(p2, dataId);
            }}
            function setPane(paneDiv, dataId) {{
                var obj = JSON.parse(jsonData[dataId]);
                paneDiv.value = obj.content;
            }}

            var jsonData = [
{jsonData}
            ];
        </script>
    </head>
    <body>
        <div class=""row"">
            <div class=""column left"">
                 <h2>{this.SelectionTitle}</h2>
                    <input type =""text"" id=""filterInput"" onkeyup=""filterInputCallback()"" placeholder=""Filter to..."">
                    <div class=""constrainedDiv"">
                        <ul id=""allOptions"">
{optionEntries}
                        </ul>
                    </div>
            </div>
            <div class=""column middle"">
                <h2>{this.Pane1Title}</h2>
                <textarea id=""pane1"" readOnly=""true"" class=""constrainedDiv comparePane"">
                </textarea>
            </div>
            <div class=""column right"">
                <h2>{this.Pane2Title}</h2>
                <textarea id=""pane2"" readOnly=""true"" class=""constrainedDiv comparePane"">
                </textarea>
            </div>
        </div>
    </body>
</html>
";
        }
    }
}
