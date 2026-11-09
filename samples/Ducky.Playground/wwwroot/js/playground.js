// The playground's only script. Blazor handles everything else (opening another tab is a plain link).
window.playground = {
  // Centre the drawer's marked line inside its code slab only: the drawer itself stays at its top, so the heading, the
  // summary and the guide links show first.
  reveal(id) {
    const line = document.getElementById(id);
    const pre = line && line.closest('pre');
    if (!pre) return;
    pre.scrollTop = Math.max(0, line.offsetTop - (pre.clientHeight - line.offsetHeight) / 2);
  },
  // Move focus along the drawer's file tabs.
  focus(id) {
    const element = document.getElementById(id);
    if (element) element.focus();
  },
  // In-page links (the skip link, the phone's basket pill): under the base href a bare "#id" would load another document
  // when the URL carries a query, so scroll and focus the target in place.
  jump(id) {
    const target = document.getElementById(id);
    if (!target) return;
    if (!target.hasAttribute('tabindex')) target.setAttribute('tabindex', '-1');
    target.focus({ preventScroll: true });
    target.scrollIntoView({ block: 'start' });
  },
};
