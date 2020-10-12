using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DllImportGenerator.Tools.Reporting
{
    internal static class Html
    {
        private const string Style = @"
            html, body {
                height: 100%;
                width: 100%;
                margin: 0px;
                padding: 0px;
                font-family: sans-serif;
            }
            .title {
                text-align: center;
                font-weight: bold;
                font-size: x-large;
                margin: 5px;
            }
            .subtitle {
                text-align: center;
                font-style: italic;
                font-size: large;
                margin: 5px;
            }
            .main {
                margin: 5px;
                display: flex;
            }
            .data-table {
                flex: 75%;
            }
            .group-header {
                font-weight: bold;
                font-style: italic;
                background-color: #f8f8f8;
            }
            table {
                width: 100%;
                height: 100%;
                table-layout: fixed;
                border-collapse: collapse;
            }
            th {
                padding: 5px 10px;
                font-weight: bold;
                text-align: left;
            }
            tbody {
                font-family: monospace;
            }
            td {
                padding: 5px;
                overflow-wrap: break-word;
            }
            tr { 
                background-color: #ffffff;
                border: 1px solid #cccccc;
                box-sizing: border-box;
            }
            label {
                display: flex;
                align-items: center;
                padding: 1px;
            }
            .side-bar {
                flex: 25%;
                overflow: hidden;
            }
            .ellipsis-text {
                overflow: hidden;
                text-overflow: ellipsis;
            }
            .side-bar-group {
                padding: 5px;
                margin-left: 5px;
                margin-bottom: 5px;
                border: 1px solid #cccccc;
            }
            .filter-list {
                list-style-type: none;
                margin: 0px;
                padding: 0px;
                font-family: monospace;
            }
            .filter-list > li:hover, .filter-list > li.selected {
                background-color: #eeeeee;
            }
            .filter-input {
                line-height: 1.5;
                width: 100%;
                box-sizing: border-box;
                margin: 1px 0px;
            }
            #option-select-all {
                border-bottom: 1px solid #cccccc;
            }
            .summary-row {
                display: flex;
                flex-direction: row;
                flex-wrap: wrap;
                width: 100%;
                align-items: baseline;
            }
            .summary-column {
                display: flex;
                flex-direction: column;
                flex-basis: 100%;
                flex: 1;
            }
            .right-align {
                text-align: right;
            }
            .status-indicator {
                text-align: center;
                width: 25px;
                flex: 0 0 auto;
            }
            .cross-status {
                display: none;
            }
            .table-data-row.disabled > td.cross-status {
                display: table-cell;
            }
            .table-data-row.disabled > td.check-status {
                display: none;
            }
";
        private const string Script = @"
            function filterOptions(filter) {
                let css = filter
                    ? `
                        li[data-option] {
                            display: none;
                        }
                        li[data-option*=""${filter}"" i] {
                            display: list-item;
                        }`
                    : ``;
                document.getElementById('option-filter-style').innerHTML = css;
                document.getElementById('option-select-all-checkbox').checked = false;
            }
            function filterTable() {
                // Re-enable all rows
                let allRows = document.getElementsByClassName('table-data-row');
                for (let row of allRows) {
                    row.classList.remove('disabled');
                }

                updateTableForFilter('category');
                updateTableForFilter('option');

                // Update counts
                let disabledCount = document.getElementsByClassName('table-data-row disabled').length;
                let enabledCount = document.querySelectorAll('.table-data-row:not(.disabled)').length;
                document.getElementById('enabled-total').innerHTML = enabledCount;
                document.getElementById('disabled-total').innerHTML = disabledCount;

                let total = enabledCount + disabledCount;
                document.getElementById('enabled-percent').innerHTML = (enabledCount / total * 100).toFixed() + '%';
                document.getElementById('disabled-percent').innerHTML = (disabledCount / total * 100).toFixed() + '%';
            }
            function updateTableForFilter(filterType) {
                let checkboxes = document.getElementsByClassName(`${filterType}-checkbox`);
                for (let checkbox of checkboxes) {
                    if (checkbox.checked)
                        continue;

                    let name = checkbox.dataset.name;
                    if (!name)
                        continue;

                    // Set disabled class on row
                    let rows = document.querySelectorAll(`tr[data-${filterType}-filter~=""${name}""].table-data-row`);
                    for (let row of rows) {
                        row.classList.add('disabled');
                    }
                }
            }
            function showTableData(enabled, show) {
                let style = enabled ? hideEnabledStyle : hideDisabledStyle;
                if (show) {
                    document.head.removeChild(style);
                } else {
                    document.head.appendChild(style);
                }
            }

            class FilterList {
                constructor(element, onFilterUpdateFunc, hasSelectAllFirstItem) {
                    this.list = element;
                    this.focusedIndex = -1;
                    this.selectedItems = [];
                    this.onFilterUpdateFunc = onFilterUpdateFunc;
                    this.items = [...element.children];
                    if (hasSelectAllFirstItem) {
                        this.selectAllItem = element.children[0];
                        this.selectAllIndex = -1;
                        this.items.shift();
                        this.selectAllItem.addEventListener('change', this.onSelectAllChanged.bind(this));
                    }
                    element.addEventListener('keydown', this.onKeyDown.bind(this));
                    element.addEventListener('click', this.onClick.bind(this));
                    element.addEventListener('focus', this.resetFocus.bind(this));
                    element.addEventListener('focusout', this.focusOut.bind(this));
                }
                onKeyDown(event) {
                    if (event.key === 'ArrowUp' || event.key === 'ArrowDown') {
                        let down = event.key === 'ArrowDown';
                        this.focusNextItem(this.focusedIndex, down, event.shiftKey);
                        event.preventDefault();
                        return false;
                    } else if (event.key === ' ' && this.selectedItems.length !== 0) {
                        for (let item of this.selectedItems) {
                            let inputs = item.getElementsByTagName('input');
                            if (inputs.length !== 0 && inputs[0]) {
                                let newValue = !inputs[0].checked;
                                inputs[0].checked = newValue;
                                if (item === this.selectAllItem) {
                                    this.checkAll(newValue);
                                    break;
                                }
                            }
                        }
                        event.preventDefault();
                        this.onFilterUpdateFunc();
                        return false;
                    } else if (event.key === 'a' && event.ctrlKey) {
                        for (let [index, item] of this.items.entries()) {
                            if (getComputedStyle(item).display === 'none')
                                continue;

                            this.selectItem(index, false);
                        }
                        event.preventDefault();
                        return false;
                    }
                    return true;
                }
                onSelectAllChanged(event) {
                    let checked = event.target.checked;
                    this.checkAll(checked);
                    this.onFilterUpdateFunc();
                }
                checkAll(checked) {
                    for (let item of this.items) {
                        if (getComputedStyle(item).display === 'none')
                            continue;

                        let inputs = item.getElementsByTagName('input');
                        if (inputs.length !== 0 && inputs[0]) {
                            inputs[0].checked = checked;
                        }
                    }
                }
                resetFocus(event) {
                    if (this.list.contains(event.relatedTarget))
                        return;

                    // Focus on first item
                    if (this.selectAllItem) {
                        this.selectItem(-1, true);
                    } else {
                        this.focusNextItem(-1, true, false);
                    }
                    if (event)
                        event.preventDefault();
                }
                focusOut(event) {
                    if (!this.list.contains(event.relatedTarget)) {
                        this.clearSelection();
                    }
                }
                onClick(event) {
                    let item = event.target.closest('li');
                    let index = this.items.indexOf(item)
                    if (index !== -1) {
                        this.clearSelection();
                        this.selectItem(index, true);
                    }
                };
                focusNextItem(startIndex, increment, addToSelection) {
                    if (startIndex == this.selectAllIndex && this.selectAllItem && addToSelection) {
                        return;
                    }

                    let delta = increment ? 1 : -1;
                    let index = startIndex + delta;
                    let focusedIndexMaybe;
                    if (index == this.selectAllIndex && this.selectAllItem && !addToSelection) {
                        focusedIndexMaybe = index;
                    } else {
                        if (index < 0 || index >= this.items.length)
                            return;

                        // Get next visible item
                        for (let i = startIndex + delta; i < this.items.length; i+= delta) {
                            let itemMaybe = this.items[i];
                            if (getComputedStyle(itemMaybe).display === 'none')
                                continue;

                            focusedIndexMaybe = i;
                            break;
                        }
                    }
                    
                    if (typeof focusedIndexMaybe !== 'undefined') {
                        if (!addToSelection) {
                            this.clearSelection();
                        }

                        let isNewlySelected = this.selectItem(focusedIndexMaybe, true);
                        console.log('isNewlySelected: ' + isNewlySelected + ', newSelection: ' + focusedIndexMaybe + ', addToSelection: ' + addToSelection);
                        if (addToSelection && !isNewlySelected && startIndex >= 0 && startIndex < this.items.length) {
                            let previous = this.items[startIndex];
                            previous.classList.remove('selected');
                            this.selectedItems.splice(this.selectedItems.indexOf(previous), 1);
                        }
                    }
                }
                selectItem(index, setFocus) {
                    let item;
                    if (index == this.selectAllIndex && this.selectAllItem) {
                        item = this.selectAllItem;
                    } else {
                        if (index < 0 || index >= this.items.length)
                            return;

                        item = this.items[index];
                    }

                    if (setFocus) {
                        item.focus();
                        this.focusedIndex = index;
                    }

                    if (this.selectedItems.includes(item))
                        return false;

                    item.classList.add('selected');
                    this.selectedItems.push(item);
                    return true;
                }
                clearSelection() {
                    for (let item of this.selectedItems) {
                        item.classList.remove('selected');
                    }

                    this.selectedItems = [];
                }
            }

            (function () {
                window.addEventListener('DOMContentLoaded', function () {
                    onLoad();
                });

                function onLoad()
                {
                    let categoryList = new FilterList(document.getElementById('category-list'), filterTable);
                    let optionList = new FilterList(document.getElementById('option-list'), filterTable, true);
                }
            })();

            const hideDisabledStyle = document.createElement('style');
            hideDisabledStyle.innerHTML = `
                .table-data-row.disabled {
                    display: none;
                }`;
            const hideEnabledStyle = document.createElement('style');
            hideEnabledStyle.innerHTML = `
                .table-data-row:not(.disabled) {
                    display: none;
                }`;
";

        /// <summary>
        /// Generate an HTML report
        /// </summary>
        /// <param name="dump">P/Invoke information</param>
        /// <param name="title">Report title</param>
        /// <param name="subtitle">Report subtitle</param>
        /// <returns>HTML text</returns>
        public static string Generate(PInvokeDump dump, string title, string subtitle)
        {
            IReadOnlyDictionary<string, IReadOnlyCollection<PInvokeMethod>> importsByAssemblyPath = dump.MethodsByAssemblyPath;

            string[] headers = { "Name", "Return", "Arguments" };
            var tableHeader = new StringBuilder();
            tableHeader.Append(@$"<th class=""status-indicator"" scope=""col""></th>");
            foreach (var header in headers)
                tableHeader.Append(@$"<th scope=""col"">{header}</th>");

            var tableBody = new StringBuilder();
            foreach ((string assemblyPath, IReadOnlyCollection<PInvokeMethod> importedMethods) in importsByAssemblyPath)
            {
                if (importedMethods.Count > 0)
                {
                    tableBody.Append(@$"<tr><td colspan=""{headers.Length + 1}"" class=""group-header"">{System.IO.Path.GetFileName(assemblyPath)} (total: {importedMethods.Count})</td></tr>");
                    foreach (var method in importedMethods)
                    {
                        var indirectionValues = method.ArgumentTypes.Select(t => t.Indirection).Append(method.ReturnType.Indirection).Distinct();
                        var nameValues = method.ArgumentTypes.Select(t => $"{t.Name}").Append(method.ReturnType.Name).Distinct();
                        tableBody.Append(
                            CreateTableRow(
                                categoryFilter: string.Join(' ', indirectionValues),
                                optionFilter: string.Join(' ', nameValues),
                                $"{method.EnclosingTypeName}<br/>{method.MethodName}",
                                method.ReturnType.ToString(),
                                string.Join("<br/>", method.ArgumentTypes.Select(t => t.ToString()))));
                    }
                }
            }

            string filters = CreateFilters(Enum.GetValues<ParameterInfo.IndirectKind>().Select(i => i.ToString()), dump.AllTypeNames.OrderBy(s => s));

            int total = dump.Count;
            return @$"
<!DOCTYPE html>
<html>
    <head>
        <title>{title} - {subtitle}</title>
        <style>
            {Style}
        </style>
        <style id=""option-filter-style""></style>
        <script>
            {Script}
        </script>
    </head>
    <body>
        <div class=""title"">{title}</div>
        <div class=""subtitle"">{subtitle}</div>
        <div class=""main"">
            <div class=""data-table"">
                <table>
                    <thead>
                        <tr>
                            {tableHeader}
                        </tr>
                    </thead>
                    <tbody>
                        {tableBody}
                    </tbody>
                </table>
            </div>
            <div class=""side-bar"">
                <div class=""side-bar-group"">
                    <div class=""summary-row"">
                        <div class=""summary-column status-indicator"">&#10033;</div>
                        <div class=""summary-column"">Total</div>
                        <div class=""summary-column right-align"">{total}</div>
                        <div class=""summary-column""></div>
                    </div>
                    <div class=""summary-row"">
                        <div class=""summary-column status-indicator"">&#10004;&#65039;</div>
                        <div class=""summary-column"">Enabled</div>
                        <div id=""enabled-total"" class=""summary-column right-align"">{total}</div>
                        <div id=""enabled-percent"" class=""summary-column right-align"">100 %</div>
                    </div>
                    <div class=""summary-row"">
                        <div class=""summary-column status-indicator"">&#10060;</div>
                        <div class=""summary-column"">Disabled</div>
                        <div id=""disabled-total"" class=""summary-column right-align"">0</div>
                        <div id=""disabled-percent"" class=""summary-column right-align"">0 %</div>
                    </div>
                </div>
                <div class=""side-bar-group"">
                    <label title=""Show disabled"">
                        <input type=""checkbox"" checked onclick=""showTableData(false, this.checked)""/>
                        <span class=""ellipsis-text"">Show disabled</span>
                    </label>
                    <label title=""Show enabled"">
                        <input type=""checkbox"" checked onclick=""showTableData(true, this.checked)""/>
                        <span class=""ellipsis-text"">Show enabled</span>
                    </label>
                </div>  
                {filters}
            </div>
        </div>
    </body>
</html>
";
        }

        private static string CreateTableRow(string categoryFilter, string optionFilter, params string[] cellValues)
        {
            var tableRow = new StringBuilder();
            tableRow.AppendLine($@"<tr class=""table-data-row"" data-category-filter=""{categoryFilter}"" data-option-filter=""{optionFilter}"">");
            tableRow.Append(@"<td class=""status-indicator cross-status"">&#10060;</td>");
            tableRow.Append(@"<td class=""status-indicator check-status"">&#10004;&#65039;</td>");
            foreach (string value in cellValues)
                tableRow.Append($"<td>{value}</td>");
    
            tableRow.AppendLine("</tr>");
            return tableRow.ToString();
        }

        private static string CreateFilters(IEnumerable<string> categories, IEnumerable<string> options)
        {
            var categoryListItems = new StringBuilder();
            foreach (var category in categories)
            {
                categoryListItems.Append(CreateFilterListItem(category, "category"));
            }

            var optionListItems = new StringBuilder();
            optionListItems.Append(@$"
                <li id=""option-select-all"" tabindex=""-1"">
                    <label>
                        <input type=""checkbox"" tabindex=""-1"" id=""option-select-all-checkbox"" checked />
                        <span class=""ellipsis-text"">(Select all visible)</span>
                    </label>
                </li>");
            foreach (string option in options)
            {
                optionListItems.Append(CreateFilterListItem(option, "option"));
            }

            return @$"
                <div class=""side-bar-group"">
                    Categories:
                    <ul class=""filter-list"" id=""category-list"" tabindex=""0"">
                        {categoryListItems}
                    </ul>
                </div>  
                <div class=""side-bar-group"">
                    Options:
                    <input type=""text"" class=""filter-input"" oninput=""filterOptions(this.value)"" placeholder=""Filter..."" />
                    <ul class=""filter-list"" id=""option-list"" tabindex=""0"">
                        {optionListItems}
                    </ul>
                </div>
        ";
        }

        private static string CreateFilterListItem(string text, string type)
        {
            return @$"
                <li data-{type}=""{text}"" tabindex=""-1"">
                    <label title=""{text}"">
                        <input type=""checkbox"" class=""{type}-checkbox"" tabindex=""-1"" data-name=""{text}"" onclick=""filterTable()"" checked/>
                        <span class=""ellipsis-text"">{text}</span>
                    </label>
                </li>";
        }
    }
}
