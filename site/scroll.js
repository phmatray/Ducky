// Ducky site: the scroll staging. Mounts the scrollcraft engine, keeps the duck's docket in step with the transcript and
// runs the race. Everything is progressive: without this file the race stays a static ledger of the same events.
(() => {
  "use strict";

  const reduceMotion = matchMedia("(prefers-reduced-motion: reduce)").matches;
  const clamp = (x) => Math.min(1, Math.max(0, x));
  const replay = (el, cls) => { el.classList.remove(cls); void el.offsetWidth; el.classList.add(cls); };

  // Under reduced motion the race is not pinned: it stays the static ledger the markup already is.
  const race = document.getElementById("a-typing");
  if (race && reduceMotion) race.removeAttribute("data-sc-act");
  if (race && !reduceMotion) race.classList.add("is-live");
  window.ScrollCraft?.mount(document.body);

  // ---------- the docket: the duck asks whichever answer is being read ----------
  const links = [...document.querySelectorAll(".docket a")];
  const bubble = document.getElementById("rail-text");
  const duck = document.querySelector(".rail .duck");
  const answers = links.map((a) => document.querySelector(a.getAttribute("href"))).filter((el) => el?.matches(".answers .answer"));

  function ask(id) {
    const index = links.findIndex((a) => a.getAttribute("href") === `#${id}`);
    if (links[index].classList.contains("current")) return;
    links.forEach((a, i) => {
      a.classList.toggle("current", i === index);
      a.classList.toggle("done", i < index);
      if (i === index) a.setAttribute("aria-current", "true"); else a.removeAttribute("aria-current");
    });
    bubble.textContent = links[index].dataset.q;
    if (!reduceMotion) { replay(bubble, "swap"); replay(duck, "quack"); }
  }

  if ("IntersectionObserver" in window && answers.length) {
    const io = new IntersectionObserver((entries) => {
      entries.filter((e) => e.isIntersecting).forEach((e) => ask(e.target.id));
    }, { rootMargin: "-40% 0px -55% 0px" });
    answers.forEach((el) => io.observe(el));
  }

  // ---------- the race: scroll is the clock ----------
  // The state is never stored. Each frame reduces the event log up to the scroll position, so scrolling back un-happens it.
  if (!race || reduceMotion) return;

  const rows = [...race.querySelectorAll("tr[data-at]")].map((el) => ({ el, at: +el.dataset.at, type: el.dataset.type, value: el.dataset.value }));
  const beats = [...race.querySelectorAll("[data-beat]")].map((el) => ({ el, at: +el.dataset.beat }));
  const out = { paris: race.querySelector("#race-paris"), version: race.querySelector("#race-version"), dropped: race.querySelector("#race-dropped") };
  const lanes = { slow: race.querySelector('[data-run="1"]'), fast: race.querySelector('[data-run="2"]') };
  const packet = { slow: lanes.slow.querySelector(".pkt"), fast: lanes.fast.querySelector(".pkt") };

  // Where each moment sits on the clock (0 to 1 of the pinned travel).
  const CLOCK = { slowFrom: 0.06, slowTo: 0.78, fastFrom: 0.30, fastTo: 0.52, superseded: 0.26, wipeFrom: 0.78, wipeTo: 0.86 };

  function reduce(state, row) {
    switch (row.type) {
      case "loading": return { ...state, paris: "loading" };
      case "loaded": return { ...state, paris: row.value };
      case "dropped": return { ...state, dropped: state.dropped + 1 }; // refused: Paris is untouched
      default: return state; // LoadForecast: no slice reduces it, it starts the effect
    }
  }

  function render(p) {
    let state = { paris: "–", version: 0, dropped: 0 };
    for (const row of rows) {
      const on = p >= row.at;
      row.el.classList.toggle("off", !on);
      if (!on) continue;
      const next = reduce(state, row);
      state = next.paris !== state.paris ? { ...next, version: state.version + 1 } : next;
    }
    out.paris.textContent = state.paris;
    out.version.textContent = state.version;
    out.dropped.textContent = state.dropped;

    packet.slow.style.setProperty("--x", clamp((p - CLOCK.slowFrom) / (CLOCK.slowTo - CLOCK.slowFrom)).toFixed(4));
    packet.fast.style.setProperty("--x", clamp((p - CLOCK.fastFrom) / (CLOCK.fastTo - CLOCK.fastFrom)).toFixed(4));
    packet.slow.classList.toggle("waiting", p < CLOCK.slowFrom);
    packet.fast.classList.toggle("waiting", p < CLOCK.fastFrom);
    packet.fast.classList.toggle("landed", p >= CLOCK.fastTo);
    lanes.slow.classList.toggle("superseded", p >= CLOCK.superseded);

    const current = beats.filter((b) => p >= b.at).at(-1);
    beats.forEach((b) => b.el.classList.toggle("on", b === current));

    const wipe = clamp((p - CLOCK.wipeFrom) / (CLOCK.wipeTo - CLOCK.wipeFrom));
    race.style.setProperty("--wipe", wipe.toFixed(4));
    race.classList.toggle("stamped", wipe > 0);
  }

  let queued = false;
  const tick = () => {
    queued = false;
    const box = race.getBoundingClientRect();
    render(clamp(-box.top / Math.max(1, box.height - innerHeight)));
  };
  const schedule = () => { if (!queued) { queued = true; requestAnimationFrame(tick); } };
  addEventListener("scroll", schedule, { passive: true });
  addEventListener("resize", schedule);
  tick();
})();
