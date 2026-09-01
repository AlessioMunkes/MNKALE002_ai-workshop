/*
 * page-help.js
 *
 * Opens and closes the "About this page" dialog rendered by _Layout. Uses the
 * native <dialog> element, so focus trapping, Escape to close and the backdrop
 * all come from the browser rather than from code here.
 */
(function () {
    'use strict';

    function dialog() {
        return document.getElementById('page-help');
    }

    document.addEventListener('click', function (event) {
        var opener = event.target.closest('[data-help-open]');
        if (opener) {
            var element = dialog();
            if (element && typeof element.showModal === 'function') {
                element.showModal();
            }
            return;
        }

        if (event.target.closest('[data-help-close]')) {
            var open = dialog();
            if (open && open.open) { open.close(); }
            return;
        }

        // A click on the backdrop lands on the <dialog> itself rather than on
        // anything inside it, which is how we tell the two apart.
        var current = dialog();
        if (current && current.open && event.target === current) {
            current.close();
        }
    });
})();
