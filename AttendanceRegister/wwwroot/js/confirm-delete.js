/*
 * confirm-delete.js
 *
 * Upgrades any [data-confirm-delete] link into a modal confirmation.
 *
 * The link points at a page that can confirm the same action on its own, so
 * with scripting off the control still works — it just takes a navigation
 * instead of a dialog. Nothing destructive is reachable in one click either
 * way, which is the part that has to hold regardless of JavaScript.
 */
(function () {
    'use strict';

    function dialog() {
        return document.getElementById('confirm-delete');
    }

    function open(trigger) {
        var element = dialog();
        if (!element || typeof element.showModal !== 'function') {
            return false; // No dialog support: let the link navigate instead.
        }

        var title = element.querySelector('[data-confirm-title]');
        var detail = element.querySelector('[data-confirm-detail]');
        var warning = element.querySelector('[data-confirm-warning]');
        var id = element.querySelector('[data-confirm-id]');

        if (title) { title.textContent = trigger.getAttribute('data-confirm-title') || 'Delete this?'; }
        if (detail) { detail.textContent = trigger.getAttribute('data-confirm-detail') || ''; }

        var warningText = trigger.getAttribute('data-confirm-warning');
        if (warning) {
            warning.textContent = warningText || '';
            warning.hidden = !warningText;
        }

        if (id) { id.value = trigger.getAttribute('data-confirm-id') || ''; }

        element.showModal();
        return true;
    }

    document.addEventListener('click', function (event) {
        var trigger = event.target.closest('[data-confirm-delete]');
        if (trigger) {
            if (open(trigger)) { event.preventDefault(); }
            return;
        }

        if (event.target.closest('[data-confirm-cancel]')) {
            event.preventDefault();
            var open_ = dialog();
            if (open_ && open_.open) { open_.close(); }
            return;
        }

        // A click on the backdrop lands on the <dialog> itself.
        var current = dialog();
        if (current && current.open && event.target === current) {
            current.close();
        }
    });
})();
