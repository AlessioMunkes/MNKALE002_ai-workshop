/*
 * checkin-pulse.js
 *
 * Keeps a rotating check-in code current, and the tally of who has checked in
 * rising, without reloading the page.
 *
 * The countdown ring runs locally on a one-second tick. The server is asked
 * every five seconds — often enough that the tally feels live, rarely enough
 * that a projector left open for an hour makes 720 requests rather than 3600 —
 * and again immediately whenever a code step elapses.
 *
 * The page renders a correct code and tally on its own, so with scripting off
 * the screen is right for up to thirty seconds and a refresh corrects it.
 * Nothing here is load-bearing.
 */
(function () {
    'use strict';

    var POLL_EVERY_SECONDS = 5;
    var CIRCUMFERENCE = 100.53;

    function attach(panel) {
        var url = panel.getAttribute('data-pulse-url');
        if (!url) { return; }

        var step = parseInt(panel.getAttribute('data-step'), 10) || 30;
        var remaining = parseInt(panel.getAttribute('data-remaining'), 10) || step;

        var codeEl = panel.querySelector('[data-code]');
        var qrEl = panel.querySelector('[data-qr]');
        var countdownEl = panel.querySelector('[data-countdown]');
        var presentEl = panel.querySelector('[data-present]');
        var enrolledEl = panel.querySelector('[data-enrolled]');
        var fillEl = panel.querySelector('[data-tally-fill]');
        var ring = panel.querySelector('.live-code__ring-progress');

        var tick = 0;
        var fetching = false;

        function paint() {
            if (countdownEl) { countdownEl.textContent = Math.max(0, remaining); }
            if (ring) {
                var fraction = Math.max(0, Math.min(1, remaining / step));
                ring.style.strokeDashoffset = (CIRCUMFERENCE * (1 - fraction)).toFixed(2);
            }
            panel.classList.toggle('is-expiring', remaining <= 5);
        }

        function swapCode(code) {
            if (!codeEl || codeEl.textContent === code) { return; }
            codeEl.classList.remove('is-swapping');
            void codeEl.offsetWidth; // force a reflow so the animation restarts
            codeEl.textContent = code;
            codeEl.classList.add('is-swapping');
        }

        function updateTally(present, enrolled, percent) {
            if (!presentEl) { return; }

            var previous = parseInt(presentEl.textContent, 10);
            if (previous !== present) {
                presentEl.textContent = present;
                // A brief pulse marks an arrival, which is the whole point of
                // having the tally on screen during a lecture.
                presentEl.classList.remove('is-bumped');
                void presentEl.offsetWidth;
                presentEl.classList.add('is-bumped');
            }

            if (enrolledEl) { enrolledEl.textContent = enrolled; }
            if (fillEl) { fillEl.style.width = percent + '%'; }
        }

        function pulse() {
            if (fetching) { return; }
            fetching = true;

            fetch(url, { headers: { 'Accept': 'application/json' }, credentials: 'same-origin' })
                .then(function (response) {
                    if (!response.ok) { throw new Error('pulse failed'); }
                    return response.json();
                })
                .then(function (data) {
                    if (!data.isOpen) {
                        panel.classList.add('is-closed');
                        if (codeEl) { codeEl.textContent = 'Closed'; }
                        if (qrEl) { qrEl.innerHTML = ''; }
                        remaining = step;
                        paint();
                        return;
                    }

                    swapCode(data.code);
                    if (qrEl && data.qrSvg) { qrEl.innerHTML = data.qrSvg; }
                    updateTally(data.presentCount, data.enrolled, data.checkedInPercent);
                    remaining = data.secondsRemaining || step;
                    paint();
                })
                .catch(function () {
                    // A dropped request is not worth surfacing on a projector.
                    // The next tick tries again.
                    remaining = step;
                })
                .then(function () { fetching = false; });
        }

        paint();

        window.setInterval(function () {
            tick += 1;
            remaining -= 1;

            if (remaining <= 0 || tick % POLL_EVERY_SECONDS === 0) {
                pulse();
            } else {
                paint();
            }
        }, 1000);
    }

    function init() {
        var panels = document.querySelectorAll('[data-checkin-pulse]');
        Array.prototype.forEach.call(panels, attach);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
