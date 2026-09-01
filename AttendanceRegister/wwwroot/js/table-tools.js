/*
 * table-tools.js
 *
 * Progressive enhancement for any <table data-table-tools>. The server renders
 * a complete, readable table; this adds:
 *
 *   - a search box that filters rows on their text
 *   - a dropdown for every column marked data-filter
 *   - a Columns menu for hiding and showing columns
 *   - click-to-sort headings (highest first, then lowest, then original order)
 *   - a live count of matching rows
 *
 * With scripting off the table still renders in full, just without the
 * toolbar. No dependencies, no build step.
 *
 * Markup contract:
 *   <table data-table-tools data-table-id="class-list">
 *     <thead><tr>
 *       <th data-lock>Student</th>          <- cannot be hidden
 *       <th data-filter>Standing</th>       <- gets a dropdown
 *       <th data-nosort>Pattern</th>        <- not sortable
 *       <th data-label="Rate">%</th>        <- label for the Columns menu
 *     </tr></thead>
 *     <tbody>
 *       <tr data-href="/somewhere">         <- whole row becomes clickable
 *         <td data-value="Below">…</td>     <- value used for filtering
 *         <td data-sort="2026-03-09">…</td> <- value used for sorting
 *
 * Sorting reads data-sort, then data-value, then the cell text. Dates carry a
 * data-sort of yyyy-MM-dd so they sort chronologically as plain strings, which
 * avoids parsing display formats.
 */
(function () {
    'use strict';

    var STORAGE_PREFIX = 'attendanceRegister.columns.';

    function toArray(collection) {
        return Array.prototype.slice.call(collection);
    }

    function filterValue(cell) {
        if (!cell) { return ''; }
        var explicit = cell.getAttribute('data-value');
        return (explicit !== null ? explicit : cell.textContent).trim();
    }

    function sortValue(cell) {
        if (!cell) { return ''; }
        var explicit = cell.getAttribute('data-sort');
        if (explicit === null) { explicit = cell.getAttribute('data-value'); }
        return (explicit !== null ? explicit : cell.textContent).trim();
    }

    /* Returns a number, or null when the text is not purely numeric.
       Tolerates thousands separators, spaces and a trailing percent sign. */
    function asNumber(text) {
        var cleaned = text.replace(/[,\s%]/g, '');
        if (cleaned === '' || !/^-?\d*\.?\d+$/.test(cleaned)) { return null; }
        return parseFloat(cleaned);
    }

    function readStored(key) {
        try {
            var raw = window.localStorage.getItem(STORAGE_PREFIX + key);
            return raw ? JSON.parse(raw) : null;
        } catch (e) {
            return null; // Private browsing, or storage disabled. Not worth failing over.
        }
    }

    function writeStored(key, value) {
        try {
            window.localStorage.setItem(STORAGE_PREFIX + key, JSON.stringify(value));
        } catch (e) {
            /* ignore */
        }
    }

    function enhance(table, index) {
        var thead = table.tHead;
        var tbody = table.tBodies[0];
        if (!thead || !tbody || thead.rows.length === 0) { return; }

        var headRow = thead.rows[thead.rows.length - 1];
        var headCells = toArray(headRow.cells);
        var rows = toArray(tbody.rows);
        if (headCells.length === 0) { return; }

        var tableId = table.getAttribute('data-table-id') || ('table' + index);
        var hidden = readStored(tableId) || [];
        var originalOrder = rows.slice();
        var sortColumn = -1;
        var sortDirection = 0; // -1 highest first, 1 lowest first, 0 unsorted

        // ---------------------------------------------------------- toolbar --
        var host = table.closest('.table-wrap') || table;
        var tools = document.createElement('div');
        tools.className = 'table-tools';
        host.parentNode.insertBefore(tools, host);

        // Search -------------------------------------------------------------
        var searchWrap = document.createElement('div');
        searchWrap.className = 'table-tools__search';

        var searchId = 'search-' + tableId;
        var searchLabel = document.createElement('label');
        searchLabel.className = 'sr-only';
        searchLabel.setAttribute('for', searchId);
        searchLabel.textContent = 'Search this table';

        var search = document.createElement('input');
        search.type = 'search';
        search.id = searchId;
        search.className = 'input input--sm';
        search.placeholder = 'Search…';
        search.autocomplete = 'off';

        searchWrap.appendChild(searchLabel);
        searchWrap.appendChild(search);
        tools.appendChild(searchWrap);

        // Filters ------------------------------------------------------------
        var filters = [];
        headCells.forEach(function (th, columnIndex) {
            if (!th.hasAttribute('data-filter')) { return; }

            var label = (th.getAttribute('data-label') || th.textContent).trim();
            var values = [];
            rows.forEach(function (row) {
                var value = filterValue(row.cells[columnIndex]);
                if (value && values.indexOf(value) === -1) { values.push(value); }
            });
            if (values.length < 2) { return; } // A dropdown with one option is noise.
            values.sort();

            var wrap = document.createElement('div');
            wrap.className = 'table-tools__filter';

            var selectId = 'filter-' + tableId + '-' + columnIndex;
            var selectLabel = document.createElement('label');
            selectLabel.setAttribute('for', selectId);
            selectLabel.textContent = label;

            var select = document.createElement('select');
            select.id = selectId;
            select.className = 'input input--sm';

            var all = document.createElement('option');
            all.value = '';
            all.textContent = 'All';
            select.appendChild(all);

            values.forEach(function (value) {
                var option = document.createElement('option');
                option.value = value;
                option.textContent = value;
                select.appendChild(option);
            });

            wrap.appendChild(selectLabel);
            wrap.appendChild(select);
            tools.appendChild(wrap);

            filters.push({ index: columnIndex, select: select });
            select.addEventListener('change', apply);
        });

        // Column visibility ---------------------------------------------------
        var toggleable = headCells
            .map(function (th, i) { return { th: th, index: i }; })
            .filter(function (entry) { return !entry.th.hasAttribute('data-lock'); });

        if (toggleable.length > 0) {
            var details = document.createElement('details');
            details.className = 'table-tools__columns';

            var summary = document.createElement('summary');
            summary.textContent = 'Columns';
            details.appendChild(summary);

            var panel = document.createElement('div');
            panel.className = 'table-tools__panel';

            toggleable.forEach(function (entry) {
                var label = document.createElement('label');
                var box = document.createElement('input');
                box.type = 'checkbox';
                box.checked = hidden.indexOf(entry.index) === -1;
                box.addEventListener('change', function () {
                    setColumn(entry.index, box.checked);
                    hidden = headCells
                        .map(function (_, i) { return i; })
                        .filter(function (i) { return headCells[i].classList.contains('is-col-hidden'); });
                    writeStored(tableId, hidden);
                });

                var text = document.createElement('span');
                text.textContent = columnLabel(entry.th);

                label.appendChild(box);
                label.appendChild(text);
                panel.appendChild(label);
            });

            details.appendChild(panel);
            tools.appendChild(details);
        }

        // Count and reset -----------------------------------------------------
        var count = document.createElement('p');
        count.className = 'table-tools__count';
        count.setAttribute('aria-live', 'polite');
        tools.appendChild(count);

        var reset = document.createElement('button');
        reset.type = 'button';
        reset.className = 'btn btn--sm btn--ghost';
        reset.textContent = 'Reset';
        reset.addEventListener('click', function () {
            search.value = '';
            filters.forEach(function (filter) { filter.select.value = ''; });
            toggleable.forEach(function (entry) { setColumn(entry.index, true); });
            toArray(tools.querySelectorAll('.table-tools__panel input')).forEach(function (box) {
                box.checked = true;
            });
            hidden = [];
            writeStored(tableId, hidden);
            sortColumn = -1;
            sortDirection = 0;
            reorder(originalOrder);
            markSortedHeading();
            apply();
        });
        tools.appendChild(reset);

        // Empty state ---------------------------------------------------------
        var emptyRow = document.createElement('tr');
        emptyRow.className = 'table-tools__empty';
        var emptyCell = document.createElement('td');
        emptyCell.colSpan = headCells.length;
        emptyCell.textContent = 'Nothing matches what you searched for.';
        emptyRow.appendChild(emptyCell);
        emptyRow.hidden = true;
        tbody.appendChild(emptyRow);

        // ------------------------------------------------------------ sorting --
        function columnLabel(th) {
            var explicit = th.getAttribute('data-label');
            if (explicit) { return explicit.trim(); }
            var button = th.querySelector('.table-sort__label');
            return (button ? button.textContent : th.textContent).trim();
        }

        // Wrap each sortable heading's contents in a real button, so the
        // control is reachable by keyboard and announced as one.
        headCells.forEach(function (th, columnIndex) {
            if (th.hasAttribute('data-nosort')) { return; }

            var button = document.createElement('button');
            button.type = 'button';
            button.className = 'table-sort';

            var label = document.createElement('span');
            label.className = 'table-sort__label';
            while (th.firstChild) { label.appendChild(th.firstChild); }
            button.appendChild(label);

            var arrow = document.createElement('span');
            arrow.className = 'table-sort__arrow';
            arrow.setAttribute('aria-hidden', 'true');
            button.appendChild(arrow);

            th.appendChild(button);
            th.setAttribute('aria-sort', 'none');

            button.addEventListener('click', function () {
                if (sortColumn !== columnIndex) {
                    sortColumn = columnIndex;
                    sortDirection = -1;          // first click: highest to lowest
                } else if (sortDirection === -1) {
                    sortDirection = 1;           // second click: lowest to highest
                } else {
                    sortColumn = -1;             // third click: back to how it came
                    sortDirection = 0;
                }

                reorder(sortDirection === 0 ? originalOrder : sortedRows(columnIndex, sortDirection));
                markSortedHeading();
            });
        });

        function sortedRows(columnIndex, direction) {
            // A column is numeric only if every value in it is. One stray
            // dash would otherwise push the whole column into text order.
            var numeric = rows.every(function (row) {
                var value = sortValue(row.cells[columnIndex]);
                return value === '' || asNumber(value) !== null;
            });

            return rows.slice().sort(function (a, b) {
                var left = sortValue(a.cells[columnIndex]);
                var right = sortValue(b.cells[columnIndex]);

                // Blanks sit at the bottom whichever way the column is sorted.
                if (left === '' && right === '') { return 0; }
                if (left === '') { return 1; }
                if (right === '') { return -1; }

                var result = numeric
                    ? asNumber(left) - asNumber(right)
                    : left.localeCompare(right, undefined, { numeric: true, sensitivity: 'base' });

                return direction === -1 ? -result : result;
            });
        }

        function reorder(order) {
            var fragment = document.createDocumentFragment();
            order.forEach(function (row) { fragment.appendChild(row); });
            fragment.appendChild(emptyRow);
            tbody.appendChild(fragment);
        }

        function markSortedHeading() {
            headCells.forEach(function (th, columnIndex) {
                if (th.hasAttribute('data-nosort')) { return; }
                var state = columnIndex !== sortColumn
                    ? 'none'
                    : (sortDirection === -1 ? 'descending' : 'ascending');
                th.setAttribute('aria-sort', state);
            });
        }

        // ------------------------------------------------------------ logic --
        function setColumn(columnIndex, visible) {
            headCells[columnIndex].classList.toggle('is-col-hidden', !visible);
            rows.forEach(function (row) {
                var cell = row.cells[columnIndex];
                if (cell) { cell.classList.toggle('is-col-hidden', !visible); }
            });
        }

        function searchTextFor(row) {
            if (row._tableToolsText === undefined) {
                row._tableToolsText = toArray(row.cells).map(filterValue).join(' ').toLowerCase();
            }
            return row._tableToolsText;
        }

        function apply() {
            var term = search.value.trim().toLowerCase();
            var shown = 0;

            rows.forEach(function (row) {
                var visible = term === '' || searchTextFor(row).indexOf(term) !== -1;

                if (visible) {
                    for (var i = 0; i < filters.length; i++) {
                        var wanted = filters[i].select.value;
                        if (wanted !== '' && filterValue(row.cells[filters[i].index]) !== wanted) {
                            visible = false;
                            break;
                        }
                    }
                }

                row.hidden = !visible;
                if (visible) { shown++; }
            });

            emptyRow.hidden = shown !== 0;
            count.textContent = shown === rows.length
                ? shown + (rows.length === 1 ? ' row' : ' rows')
                : 'Showing ' + shown + ' of ' + rows.length;
        }

        // Row click-through, without stealing clicks from real controls.
        rows.forEach(function (row) {
            var href = row.getAttribute('data-href');
            if (!href) { return; }
            row.classList.add('is-clickable');
            row.addEventListener('click', function (event) {
                if (event.target.closest('a, button, input, select, textarea, label')) { return; }
                window.location.href = href;
            });
        });

        // Restore stored column state, then draw.
        hidden.forEach(function (columnIndex) {
            if (headCells[columnIndex] && !headCells[columnIndex].hasAttribute('data-lock')) {
                setColumn(columnIndex, false);
            }
        });

        search.addEventListener('input', apply);
        apply();
    }

    function init() {
        toArray(document.querySelectorAll('table[data-table-tools]')).forEach(enhance);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
