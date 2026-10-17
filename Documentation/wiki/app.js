// Marks the chapter currently being read in the table of contents.
const links = [...document.querySelectorAll('.toc a')];
const sections = links.map(a => document.getElementById(a.hash.slice(1)));
let current;
function update() {
  const line = innerHeight * .3;
  const index = Math.max(0, sections.findLastIndex(s => s.getBoundingClientRect().top <= line));
  const top = sections[0].getBoundingClientRect().top > line;
  const next = top ? null : links[index];
  if (next === current) return;
  current?.removeAttribute('aria-current');
  current = next;
  if (!current) return;
  current.setAttribute('aria-current', 'location');
  current.scrollIntoView({ block: 'nearest', inline: 'nearest' });
}
const observer = new IntersectionObserver(update, { rootMargin: '-30% 0px -69% 0px' });
sections.forEach(s => observer.observe(s));
update();
