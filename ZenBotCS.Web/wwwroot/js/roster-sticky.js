// Toggles an "at-right" class on the roster's horizontal scroll container when it is
// scrolled (almost) all the way right, used to expand the pinned right-most column.
//
// The expanded column is wider, which grows scrollWidth — comparing against the *current*
// scrollWidth would oscillate (expand -> wider -> "not at end" -> collapse -> ...). So we
// capture the COLLAPSED width once as a fixed reference and compare against that.
window.initRosterStickyRight = (el) => {
    if (!el || el._stickyRightInit) return;
    el._stickyRightInit = true;

    let baseWidth = el.scrollWidth; // collapsed reference width

    const update = () => {
        const nearEnd = el.scrollLeft + el.clientWidth >= baseWidth - 6;
        el.classList.toggle('at-right', nearEnd);
    };

    el.addEventListener('scroll', update, { passive: true });

    window.addEventListener('resize', () => {
        // Re-measure the collapsed width after a layout change.
        el.classList.remove('at-right');
        requestAnimationFrame(() => { baseWidth = el.scrollWidth; update(); });
    });

    update();
};
