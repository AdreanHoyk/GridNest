const card = document.querySelector('#demo-card');
const hexLabel = document.querySelector('#hex-label');
const colorInput = document.querySelector('#custom-color');
const opacityInput = document.querySelector('#demo-opacity');
const glowInput = document.querySelector('#demo-glow');
const pulseInput = document.querySelector('#demo-pulse');
let currentColor = colorInput.value;

function updateAppearance() {
  const transparency = Number(opacityInput.value);
  const channels = [1, 3, 5].map(offset => parseInt(currentColor.slice(offset, offset + 2), 16));
  card.style.setProperty('--panel-fill', 'rgba(' + channels.join(',') + ',' + (1 - transparency / 100) + ')');
  const inner = 'inset 0 0 22px #8ae5d580';
  const outer = '0 0 24px #8ae5d580';
  card.style.setProperty('--halo', glowInput.value === 'inside' ? inner : glowInput.value === 'outside' ? outer : inner + ',' + outer);
  card.classList.toggle('pulsing', pulseInput.checked);
  document.querySelector('#opacity-value').textContent = transparency + '%';
  opacityInput.setAttribute('aria-valuetext', transparency + ' процентов прозрачности');
  hexLabel.textContent = currentColor.toUpperCase();
  document.querySelectorAll('.swatch').forEach(button => {
    const active = button.dataset.color.toLowerCase() === currentColor.toLowerCase();
    button.classList.toggle('active', active);
    button.setAttribute('aria-pressed', String(active));
  });
}

document.querySelectorAll('.swatch').forEach(button => button.addEventListener('click', () => {
  currentColor = button.dataset.color;
  colorInput.value = currentColor;
  updateAppearance();
}));
colorInput.addEventListener('input', () => {
  currentColor = colorInput.value;
  updateAppearance();
});
[opacityInput, glowInput, pulseInput].forEach(input => input.addEventListener('input', updateAppearance));
updateAppearance();

const screenshotTabs = [...document.querySelectorAll('[data-shot]')];
function selectScreenshot(button) {
  screenshotTabs.forEach(tab => {
    const active = tab === button;
    tab.setAttribute('aria-selected', String(active));
    tab.tabIndex = active ? 0 : -1;
  });
  const image = document.querySelector('#interface-image');
  image.src = button.dataset.shot;
  image.alt = button.dataset.alt;
  document.querySelector('#shot-panel').setAttribute('aria-labelledby', button.id);
  document.querySelector('#shot-caption').textContent = button.dataset.caption;
}
screenshotTabs.forEach((button, index) => {
  button.addEventListener('click', () => selectScreenshot(button));
  button.addEventListener('keydown', event => {
    const last = screenshotTabs.length - 1;
    let next;
    if (event.key === 'ArrowRight') next = (index + 1) % screenshotTabs.length;
    if (event.key === 'ArrowLeft') next = (index + last) % screenshotTabs.length;
    if (event.key === 'Home') next = 0;
    if (event.key === 'End') next = last;
    if (next === undefined) return;
    event.preventDefault();
    selectScreenshot(screenshotTabs[next]);
    screenshotTabs[next].focus();
  });
});

document.querySelector('#copy').addEventListener('click', async event => {
  const button = event.currentTarget;
  const command = 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\\Install.ps1';
  try {
    await navigator.clipboard.writeText(command);
    button.textContent = 'Скопировано';
    document.querySelector('#status').textContent = 'Команда скопирована';
    setTimeout(() => { button.textContent = 'Копировать'; }, 1800);
  } catch {
    button.textContent = 'Выделите команду';
    document.querySelector('#status').textContent = 'Скопируйте команду вручную';
  }
});
