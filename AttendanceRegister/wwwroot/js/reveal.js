/*
 * reveal.js
 *
 * Fades each content block in as it scrolls into view.
 *
 * Blocks that are already on screen when the page loads are shown at once,
 * with no transition. Animating content the reader can already see delays a
 * screen they did not have to wait for, and this application is one somebody
 * opens dozens of times a day. Motion is reserved for blocks that genuinely
 * arrive: the ones scrolled to.
 *
 * Two things keep this honest:
 *
 *  - The blocks are the direct children of <main>. Nothing is annotated, so no
 *    page has to remember to opt in, and there is no second list of selectors
 *    to keep in step with the markup.
 *  - The hiding is done in CSS under html.has-js, set by an inline script in
 *    <head>. If scripting is off, or this file fails to load, every block is
 *    simply visible. Nothing is ever hidden that cannot be shown again.
 */
(function () {
    'use strict';

    var VISIBLE = 'is-revealed';
    var IMMEDIATE = 'is-immediate';

    function revealAll(blocks) {
        for (var i = 0; i < blocks.length; i++) {
            blocks[i].classList.add(IMMEDIATE, VISIBLE);
        }
    }

    function init() {
        var main = document.getElementById('main');
        if (!main) { return; }

        var blocks = Array.prototype.slice.call(main.children);
        if (blocks.length === 0) { return; }

        var wantsStillness = window.matchMedia &&
            window.matchMedia('(prefers-reduced-motion: reduce)').matches;

        if (wantsStillness || typeof IntersectionObserver === 'undefined') {
            revealAll(blocks);
            return;
        }

        var fold = window.innerHeight || document.documentElement.clientHeight;
        var deferred = [];

        blocks.forEach(function (block) {
            // Anything whose top edge is already within the viewport was not
            // being waited for. Show it with no transition at all.
            if (block.getBoundingClientRect().top < fold) {
                block.classList.add(IMMEDIATE, VISIBLE);
            } else {
                deferred.push(block);
            }
        });

        if (deferred.length === 0) { return; }

        var observer = new IntersectionObserver(function (entries) {
            entries.forEach(function (entry) {
                if (!entry.isIntersecting) { return; }
                entry.target.classList.add(VISIBLE);
                observer.unobserve(entry.target); // reveal once, not on every scroll past
            });
        }, {
            // Start the fade slightly before the block reaches the viewport, so
            // it has finished by the time it is properly in view.
            rootMargin: '0px 0px -40px 0px',
            threshold: 0.04
        });

        deferred.forEach(function (block) { observer.observe(block); });

        // Belt and braces: if anything above went wrong, nothing stays hidden.
        window.setTimeout(function () { revealAll(deferred); }, 2500);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
