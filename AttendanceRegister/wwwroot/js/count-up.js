/*
 * count-up.js
 *
 * Animates any [data-countup] element from data-countup-from to
 * data-countup-to. The element already contains the final value when it
 * arrives from the server, so with scripting off, or with reduced motion
 * requested, the number is simply correct and never animates.
 */
(function () {
    'use strict';

    var DURATION = 900;

    function ease(t) {
        // Fast out, settle in. Matches the --ease curve used in the stylesheet.
        return 1 - Math.pow(1 - t, 3);
    }

    function run(element) {
        var to = parseFloat(element.getAttribute('data-countup-to'));
        if (isNaN(to)) { return; }

        var from = parseFloat(element.getAttribute('data-countup-from'));
        if (isNaN(from)) { from = 0; }

        var decimals = parseInt(element.getAttribute('data-countup-decimals'), 10);
        if (isNaN(decimals)) { decimals = 0; }

        var suffix = element.getAttribute('data-countup-suffix') || '';
        var started = null;

        function frame(now) {
            if (started === null) { started = now; }
            var progress = Math.min(1, (now - started) / DURATION);
            var value = from + (to - from) * ease(progress);

            element.textContent = value.toFixed(decimals) + suffix;

            if (progress < 1) {
                window.requestAnimationFrame(frame);
            } else {
                element.textContent = to.toFixed(decimals) + suffix;
            }
        }

        window.requestAnimationFrame(frame);
    }

    function init() {
        var stillness = window.matchMedia &&
            window.matchMedia('(prefers-reduced-motion: reduce)').matches;

        if (stillness || !window.requestAnimationFrame) { return; }

        var targets = document.querySelectorAll('[data-countup]');
        Array.prototype.forEach.call(targets, run);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
