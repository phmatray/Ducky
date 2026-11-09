// Ducky docs: search, copy buttons, heading anchors, the on-this-page marker and the folding ledger.
// Everything is progressive: without this file every page, link and snippet still works; only search and copy are absent.
(() => {
  "use strict";

  const root = document.currentScript?.dataset.root ?? "";
  const announce = (text) => { const el = document.getElementById("announce"); if (el) el.textContent = text; };
  const escape = (t) => t.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");

  // ---------- copy ----------
  document.querySelectorAll("figure.code").forEach((figure) => {
    const code = figure.querySelector("pre code");
    if (!code) return;
    const button = document.createElement("button");
    button.type = "button";
    button.className = "copy";
    button.textContent = "Copy";
    button.setAttribute("aria-label", "Copy this snippet");
    button.addEventListener("click", async () => {
      try {
        await navigator.clipboard.writeText(code.textContent);
        button.classList.add("copied");
        button.textContent = "Copied";
        announce("Snippet copied.");
        setTimeout(() => { button.classList.remove("copied"); button.textContent = "Copy"; }, 2100);
      } catch {
        const range = document.createRange();
        range.selectNodeContents(code);
        getSelection().removeAllRanges();
        getSelection().addRange(range);
        button.textContent = "Selected";
      }
    });
    figure.append(button);
  });

  // ---------- copy the page as Markdown (the .md twin the Docs target writes beside each page) ----------
  const mdLink = document.querySelector("a[data-markdown]");
  if (mdLink && window.fetch) {
    const copyMd = document.createElement("button");
    copyMd.type = "button";
    copyMd.className = "edit";
    copyMd.textContent = "Copy page as Markdown";
    copyMd.addEventListener("click", async () => {
      try {
        const text = await (await fetch(mdLink.href)).text();
        await navigator.clipboard.writeText(text);
        copyMd.textContent = "Copied";
        announce("Page copied as Markdown.");
      } catch {
        copyMd.textContent = "Copy failed: use View as Markdown";
      }
      setTimeout(() => { copyMd.textContent = "Copy page as Markdown"; }, 2100);
    });
    mdLink.after(copyMd);
  }

  // ---------- heading anchors ----------
  document.querySelectorAll(".prose :is(h2, h3, h4)[id]").forEach((heading) => {
    const link = document.createElement("a");
    link.className = "anchor";
    link.href = `#${heading.id}`;
    link.textContent = "#";
    link.setAttribute("aria-label", `Link to ${heading.textContent}`);
    heading.append(link);
  });

  // ---------- the current tab stays in view in the scrolling strip (phones and tablets) ----------
  const strip = document.querySelector(".tabs");
  const currentTab = strip?.querySelector('a[aria-current="true"]');
  const centreTab = () => {
    if (!strip || !currentTab || strip.scrollWidth <= strip.clientWidth) return;
    const offset = currentTab.getBoundingClientRect().left - strip.getBoundingClientRect().left + strip.scrollLeft;
    strip.scrollLeft = offset - (strip.clientWidth - currentTab.offsetWidth) / 2;
  };
  centreTab();
  // Only a width change re-centres: phones fire resize when the URL bar shows or hides, which must not undo a manual scroll.
  let lastWidth = innerWidth;
  addEventListener("resize", () => { if (innerWidth !== lastWidth) { lastWidth = innerWidth; centreTab(); } }, { passive: true });

  // ---------- the ledger folds on narrow screens ----------
  const ledger = document.querySelector(".ledger");
  const narrow = matchMedia("(max-width: 960px)");
  const fold = () => { if (ledger) ledger.open = !narrow.matches; };
  fold();
  narrow.addEventListener("change", fold);
  ledger?.querySelector("summary")?.addEventListener("click", (e) => { if (!narrow.matches) e.preventDefault(); });

  // ---------- on this page: mark the section being read ----------
  // The current section is the last heading at or above a line 30% down the viewport; a hidden H3 entry (narrow screens)
  // marks its parent H2 instead.
  const tocLinks = [...document.querySelectorAll(".toc a[href^='#']")];
  if (tocLinks.length) {
    const entries = tocLinks.map((a) => ({ a, h: document.getElementById(decodeURIComponent(a.hash.slice(1))) })).filter((e) => e.h);
    let frame = 0;
    const update = () => {
      frame = 0;
      const line = innerHeight * 0.3;
      let current = null;
      for (const e of entries) {
        if (e.h.getBoundingClientRect().top <= line) current = e;
        else break;
      }
      if (current && current.a.offsetParent === null) {
        const index = entries.indexOf(current);
        current = entries.slice(0, index).reverse().find((e) => e.a.offsetParent !== null) ?? null;
      }
      tocLinks.forEach((a) => (current && a === current.a ? a.setAttribute("aria-current", "true") : a.removeAttribute("aria-current")));
    };
    addEventListener("scroll", () => { if (!frame) frame = requestAnimationFrame(update); }, { passive: true });
    addEventListener("resize", () => { if (!frame) frame = requestAnimationFrame(update); }, { passive: true });
    update();
  }

  // ---------- search ----------
  const form = document.querySelector(".search");
  const input = document.getElementById("search-input");
  const list = document.getElementById("search-results");
  if (!form || !input || !list || !window.fetch) return;
  form.hidden = false;

  let index = null;
  let loading = null;
  let results = [];
  let active = -1;
  const load = () => loading ??= fetch(`${root}_assets/search.json`).then((r) => r.json()).then((data) => {
    index = data.map((d) => ({ ...d, tl: d.t.toLowerCase(), hl: d.h.toLowerCase(), xl: d.x.toLowerCase() }));
  }).catch(() => { index = []; });

  const terms = (q) => q.toLowerCase().split(/\s+/).filter((t) => t.length > 0);
  function score(entry, words) {
    let total = 0;
    for (const w of words) {
      let s = 0;
      if (entry.tl === w) s = 120;
      else if (entry.tl.startsWith(w)) s = 70;
      else if (entry.tl.includes(w)) s = 50;
      if (entry.hl.includes(w)) s = Math.max(s, entry.hl.startsWith(w) ? 45 : 35);
      if (s === 0 && entry.xl.includes(w)) s = 10 + Math.min(10, entry.xl.split(w).length - 1);
      if (s === 0) return 0;
      total += s;
    }
    return total + (entry.h === "" ? 4 : 0);
  }
  // Marks the query words in the raw text, then escapes each piece: matching never reaches into an entity or a tag.
  function highlight(text, words) {
    const lower = text.toLowerCase();
    const marks = new Array(text.length).fill(false);
    for (const w of words) {
      if (w.length < 2) continue;
      for (let at = lower.indexOf(w); at >= 0; at = lower.indexOf(w, at + w.length)) marks.fill(true, at, at + w.length);
    }
    let html = "";
    for (let i = 0; i < text.length;) {
      let j = i;
      while (j < text.length && marks[j] === marks[i]) j++;
      html += marks[i] ? `<mark>${escape(text.slice(i, j))}</mark>` : escape(text.slice(i, j));
      i = j;
    }
    return html;
  }
  function excerpt(text, words) {
    const lower = text.toLowerCase();
    const at = Math.min(...words.map((w) => lower.indexOf(w)).filter((i) => i >= 0), Infinity);
    if (!isFinite(at) || at < 60) return text.slice(0, 150) + (text.length > 150 ? "…" : "");
    const start = text.lastIndexOf(" ", at - 40) + 1;
    return "…" + text.slice(start, start + 150) + (start + 150 < text.length ? "…" : "");
  }

  function render() {
    const words = terms(input.value);
    if (!words.length || !index) { close(); return; }
    const seen = new Set();
    results = index.map((e) => ({ e, s: score(e, words) })).filter((r) => r.s > 0)
      .sort((a, b) => b.s - a.s)
      .filter((r) => { const key = r.e.u; if (seen.has(key)) return false; seen.add(key); return true; })
      .slice(0, 12);
    active = results.length ? 0 : -1;
    list.innerHTML = results.length
      ? results.map((r, i) => `<a class="result" id="result-${i}" role="option" aria-selected="${i === active}" href="${root}${r.e.u}">` +
          `<span class="result-title">${highlight(r.e.t, words)}<span class="result-tab">${escape(r.e.s)}</span></span>` +
          `<span class="result-heading">${r.e.h ? highlight(r.e.h, words) : "Overview"}</span>` +
          `<span class="result-text">${highlight(excerpt(r.e.x, words), words)}</span></a>`).join("")
      : `<p class="empty">No page mentions “${escape(input.value.trim())}”.</p>`;
    list.hidden = false;
    input.setAttribute("aria-expanded", "true");
    input.setAttribute("aria-activedescendant", active >= 0 ? `result-${active}` : "");
    announce(results.length ? `${results.length} result${results.length > 1 ? "s" : ""}.` : "No results.");
  }
  function close() {
    active = -1;
    list.hidden = true;
    input.setAttribute("aria-expanded", "false");
    input.removeAttribute("aria-activedescendant");
  }
  function move(delta) {
    if (!results.length || list.hidden) return;
    active = (active + delta + results.length) % results.length;
    list.querySelectorAll(".result").forEach((el, i) => el.setAttribute("aria-selected", String(i === active)));
    input.setAttribute("aria-activedescendant", `result-${active}`);
    list.querySelector(`#result-${active}`)?.scrollIntoView({ block: "nearest" });
  }

  input.addEventListener("focus", () => load().then(() => { if (input.value) render(); }));
  input.addEventListener("input", () => load().then(render));
  input.addEventListener("keydown", (e) => {
    if (e.key === "ArrowDown") { e.preventDefault(); move(1); }
    else if (e.key === "ArrowUp") { e.preventDefault(); move(-1); }
    else if (e.key === "Escape") { if (!list.hidden) { e.preventDefault(); close(); } else input.blur(); }
  });
  form.addEventListener("submit", (e) => {
    e.preventDefault();
    const target = list.hidden ? null : list.querySelector(`#result-${active}`);
    if (target) location.href = target.href;
  });
  document.addEventListener("click", (e) => { if (!form.contains(e.target)) close(); });
  document.addEventListener("keydown", (e) => {
    const typing = /^(input|textarea|select)$/i.test(e.target.tagName) || e.target.isContentEditable;
    if (e.key === "/" && !typing && !e.metaKey && !e.ctrlKey && !e.altKey) { e.preventDefault(); input.focus(); input.select(); }
  });
})();
