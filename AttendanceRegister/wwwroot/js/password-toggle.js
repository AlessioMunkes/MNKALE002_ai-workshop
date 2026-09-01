/*
 * password-toggle.js
 *
 * Turns any [data-password-toggle] button into a Show/Hide control for the
 * password input beside it.
 *
 * The button is only added to the page by this script. With scripting off
 * there is no dead control sitting there doing nothing, and the field behaves
 * exactly as a password field always has.
 */
(function () {
    'use strict';

    var SHOW = 'Show';
    var HIDE = 'Hide';

    function build(field) {
        var input = field.querySelector('input[type="password"]');
        if (!input) { return; }

        var button = document.createElement('button');
        button.type = 'button';                       // never submits the form
        button.className = 'password-field__toggle';
        button.textContent = SHOW;
        button.setAttribute('aria-pressed', 'false');
        button.setAttribute('aria-label', 'Show password');
        button.setAttribute('aria-controls', input.id || '');

        button.addEventListener('click', function () {
            var revealed = input.type === 'text';
            input.type = revealed ? 'password' : 'text';
            button.textContent = revealed ? SHOW : HIDE;
            button.setAttribute('aria-pressed', revealed ? 'false' : 'true');
            button.setAttribute('aria-label', revealed ? 'Show password' : 'Hide password');

            // Keep the caret where it was, rather than dropping it to the start.
            var end = input.value.length;
            input.focus();
            if (input.setSelectionRange) {
                try { input.setSelectionRange(end, end); } catch (e) { /* ignore */ }
            }
        });

        field.appendChild(button);
        field.classList.add('has-toggle');
    }

    function init() {
        var fields = document.querySelectorAll('[data-password-toggle]');
        Array.prototype.forEach.call(fields, build);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
