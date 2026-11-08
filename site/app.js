// Ducky site: code highlighting, copy, tabs, the duck's questions and a model of the dispatcher.
// Everything is progressive: without this file the page shows the first answer, the first file and an empty board.
(() => {
  "use strict";

  const reduceMotion = matchMedia("(prefers-reduced-motion: reduce)").matches;
  const beat = parseFloat(getComputedStyle(document.documentElement).getPropertyValue("--beat")) || 420;
  const announce = (text) => { const el = document.getElementById("announce"); if (el) el.textContent = text; };
  const replay = (el, cls) => { el.classList.remove(cls); void el.offsetWidth; el.classList.add(cls); };

  // ---------- highlighting (C# and Razor, enough for the snippets on this page) ----------
  const KEYWORDS = new Set(("public private protected internal sealed partial static readonly override virtual abstract class record " +
    "struct interface enum new return if else foreach for while var async await using namespace true false null this base " +
    "void int string bool object long double get set init with is not and or typeof default throw try catch finally in out " +
    "ref params where yield break continue switch case").split(" "));
  const escape = (t) => t.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
  const TOKEN = /(\/\/[^\n]*)|("(?:[^"\\\n]|\\.)*")|(\[[A-Z][A-Za-z]*(?:\([^\]\n]*\))?\])|(@[a-zA-Z]+)|(<\/?[a-z][\w-]*)|\b([A-Za-z_][A-Za-z0-9_]*)\b/g;

  function highlight(code) {
    const src = code.textContent;
    let out = "", last = 0;
    for (const m of src.matchAll(TOKEN)) {
      out += escape(src.slice(last, m.index));
      const [text, com, str, attr, razor, tag, word] = m;
      if (com) out += `<span class="tk-com">${escape(com)}</span>`;
      else if (str) out += `<span class="tk-str">${escape(str)}</span>`;
      else if (attr) out += `<span class="tk-attr">${escape(attr)}</span>`;
      else if (razor) out += `<span class="tk-kw">${escape(razor)}</span>`;
      else if (tag) out += `<span class="tk-tag">${escape(tag)}</span>`;
      else if (KEYWORDS.has(word)) out += `<span class="tk-kw">${word}</span>`;
      else if (/^[A-Z]/.test(word)) out += `<span class="tk-type">${word}</span>`;
      else out += escape(text);
      last = m.index + text.length;
    }
    code.innerHTML = out + escape(src.slice(last));
  }
  document.querySelectorAll("code[data-lang]").forEach(highlight);

  // ---------- copy ----------
  document.querySelectorAll("[data-copy]").forEach((button) => {
    const label = button.querySelector(".cmd-label");
    button.addEventListener("click", async () => {
      try {
        await navigator.clipboard.writeText(button.dataset.copy);
        button.classList.add("copied");
        if (label) label.textContent = "Copied";
        announce("Install command copied.");
        setTimeout(() => { button.classList.remove("copied"); if (label) label.textContent = "Copy"; }, beat * 5);
      } catch {
        if (label) label.textContent = "Select it";
        const range = document.createRange();
        range.selectNodeContents(button.querySelector("code"));
        getSelection().removeAllRanges();
        getSelection().addRange(range);
      }
    });
  });

  // ---------- tabs (WAI-ARIA tabs, automatic activation) ----------
  function tabs(list, onSelect) {
    const items = [...list.querySelectorAll('[role="tab"]')];
    const select = (tab, focus) => {
      items.forEach((t) => {
        const on = t === tab;
        t.setAttribute("aria-selected", String(on));
        t.tabIndex = on ? 0 : -1;
        document.getElementById(t.getAttribute("aria-controls")).hidden = !on;
      });
      if (focus) tab.focus();
      onSelect?.(tab);
    };
    items.forEach((tab, i) => {
      tab.addEventListener("click", () => { if (tab.getAttribute("aria-selected") !== "true") select(tab, false); });
      tab.addEventListener("keydown", (e) => {
        const next = { ArrowRight: i + 1, ArrowDown: i + 1, ArrowLeft: i - 1, ArrowUp: i - 1, Home: 0, End: items.length - 1 }[e.key];
        if (next === undefined) return;
        e.preventDefault();
        select(items[(next + items.length) % items.length], true);
      });
    });
  }

  const duck = document.querySelector(".hero .duck");
  const bubble = document.getElementById("bubble-text");
  const qList = document.querySelector(".q-list");
  if (qList) {
    tabs(qList, (tab) => {
      bubble.textContent = tab.dataset.q;
      const panel = document.getElementById(tab.getAttribute("aria-controls"));
      if (!reduceMotion) { replay(bubble, "swap"); replay(duck, "quack"); replay(panel, "enter"); }
    });
  }
  document.querySelectorAll("[data-tabs] .tab-list").forEach((list) => tabs(list));

  // ---------- the dispatcher, modelled ----------
  // One queue, one drainer reducing one action per beat. LoadForecast starts a Switch effect keyed by city: a newer
  // request for the same city supersedes the running one, and a dispatch from a superseded run is Dropped (INV-11).
  const rowsEl = document.getElementById("board-rows");
  if (!rowsEl) return;
  const out = {
    version: document.getElementById("st-version"), count: document.getElementById("st-count"),
    paris: document.getElementById("st-paris"), oslo: document.getElementById("st-oslo"), queue: document.getElementById("st-queue"),
  };
  const FORECAST = { Paris: "14°C", Oslo: "6°C" }; // demonstration values
  let state, queue, seq, runs, timers, draining;

  function reset() {
    timers?.forEach(clearTimeout);
    state = { version: 0, count: 0, Paris: "–", Oslo: "–" };
    queue = []; seq = 0; runs = { Paris: 0, Oslo: 0 }; timers = []; draining = false;
    rowsEl.innerHTML = '<tr class="empty"><td colspan="4">The queue is empty. Dispatch something.</td></tr>';
    render();
  }

  function render() {
    out.version.textContent = state.version; out.count.textContent = state.count;
    out.paris.textContent = state.Paris; out.oslo.textContent = state.Oslo; out.queue.textContent = queue.length;
  }

  // A new row's action name flaps in character by character, like a departure board; its lamp flips when it settles.
  const flaps = (text) => reduceMotion ? escape(text)
    : [...text].map((c, i) => `<span class="ch" style="animation-delay:${i * 14}ms">${escape(c)}</span>`).join("");
  const word = (result) => result[0].toUpperCase() + result.slice(1);

  function row(label, origin, result, note) {
    rowsEl.querySelector(".empty")?.remove();
    const tr = document.createElement("tr");
    tr.innerHTML = `<td>${++seq}</td><td>${flaps(label)}${note ? ` <em>${note}</em>` : ""}</td><td>${origin}</td><td><span class="lamp ${result}">${word(result)}</span></td>`;
    rowsEl.prepend(tr);
    while (rowsEl.children.length > (matchMedia("(max-width: 640px)").matches ? 12 : 40)) rowsEl.lastElementChild.remove();
    return tr;
  }

  function settle(tr, result) {
    const lamp = tr.querySelector(".lamp");
    lamp.className = `lamp ${result}`;
    lamp.textContent = word(result);
    if (!reduceMotion) replay(lamp, "flip");
  }

  function dispatch(action, origin) {
    queue.push({ action, origin, tr: row(action.label, origin, "queued") });
    render();
    if (!draining) { draining = true; timers.push(setTimeout(drain, beat)); }
  }

  function drain() {
    const item = queue.shift();
    if (!item) { draining = false; return; }
    const before = JSON.stringify(state);
    reduce(item.action);
    if (JSON.stringify(state) !== before) state.version++;
    settle(item.tr, "reduced");
    render();
    item.action.after?.();
    timers.push(setTimeout(drain, beat));
  }

  function reduce(action) {
    switch (action.type) {
      case "Increment": state.count++; break;
      case "ForecastLoading": state[action.city] = "loading"; break;
      case "ForecastLoaded": state[action.city] = FORECAST[action.city]; break;
      default: break; // LoadForecast: no slice reduces it; it starts the effect
    }
  }

  function loadForecast(city) {
    dispatch({
      type: "LoadForecast", city, label: `LoadForecast("${city}")`,
      after: () => {
        const run = ++runs[city]; // Switch: this run supersedes the previous one for the same city
        dispatch({ type: "ForecastLoading", city, label: `ForecastLoading("${city}")` }, "Effect");
        timers.push(setTimeout(() => {
          if (run !== runs[city]) {
            if (!reduceMotion) replay(row(`ForecastLoaded("${city}")`, "Effect", "dropped", "stale run").querySelector(".lamp"), "flip");
            announce(`A superseded ${city} run tried to dispatch: dropped.`);
            return;
          }
          dispatch({ type: "ForecastLoaded", city, label: `ForecastLoaded("${city}")` }, "Effect");
        }, beat * 6));
      },
    }, "Local");
  }

  const actions = {
    inc: () => dispatch({ type: "Increment", label: "Increment" }, "Local"),
    paris: () => loadForecast("Paris"),
    oslo: () => loadForecast("Oslo"),
    burst: () => { for (let i = 0; i < 5; i++) actions.inc(); },
    reset,
  };
  document.querySelectorAll("[data-act]").forEach((b) => b.addEventListener("click", () => actions[b.dataset.act]()));
  // The page ships the settled log of the story below, so the board is never an empty box: continue from it.
  if (rowsEl.hasAttribute("data-settled")) {
    state = { version: 6, count: 2, Paris: FORECAST.Paris, Oslo: FORECAST.Oslo };
    queue = []; seq = rowsEl.children.length; runs = { Paris: 2, Oslo: 1 }; timers = []; draining = false;
    render();
  } else {
    reset();
  }
  let touched = false;
  document.querySelectorAll("[data-act]").forEach((b) => b.addEventListener("click", () => { touched = true; }, { capture: true }));

  // The first time the board scrolls into view, it plays the story the copy tells: a few actions, then Paris twice.
  const boardSec = document.querySelector(".board-sec");
  if ("IntersectionObserver" in window && boardSec) {
    const io = new IntersectionObserver((entries) => {
      if (!entries.some((e) => e.isIntersecting)) return;
      io.disconnect();
      if (touched || reduceMotion) return; // keep the visitor's own log; under reduced motion the settled log stays
      reset();
      const script = [actions.inc, actions.paris, actions.inc, actions.paris, actions.oslo];
      script.forEach((step, i) => timers.push(setTimeout(step, beat * (1 + i * 1.5))));
    }, { threshold: 0.6 });
    io.observe(document.querySelector(".board-wrap"));
  }
})();
