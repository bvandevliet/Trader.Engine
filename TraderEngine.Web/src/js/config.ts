function addRemoveHandler (row: HTMLElement): void
{
  row.querySelector<HTMLButtonElement>('.remove-row')?.addEventListener('click', () => row.remove());
}

// UX nicety only — instant feedback while typing, using the browser's JS regex engine. Not the
// enforcement mechanism: these patterns are actually compiled into a .NET Regex server-side
// (MarketCapService), a different dialect, so ConfigModel.OnPostAsync validates authoritatively
// and this check is allowed to disagree with it at the margins.
function isValidRegexPattern (pattern: string): boolean
{
  try
  {
    // eslint-disable-next-line no-new
    new RegExp(pattern, 'u');

    return true;
  }
  catch
  {
    return false;
  }
}

function addRegexValidationHandler (input: HTMLInputElement): void
{
  // eslint-disable-next-line func-style
  const validate = (): void => { input.classList.toggle('is-invalid', input.value !== '' && !isValidRegexPattern(input.value)); };

  validate();
  input.addEventListener('input', validate);
}

function buildRemoveButton (): HTMLButtonElement
{
  const button = document.createElement('button');

  button.type = 'button';
  button.className = 'btn btn-outline-danger remove-row';
  button.textContent = '×';

  return button;
}

function buildTagRow (inputName: string): HTMLDivElement
{
  const row = document.createElement('div');
  row.className = 'input-group mb-2 tag-row';

  const prefix = document.createElement('span');
  prefix.className = 'input-group-text font-monospace';
  prefix.textContent = '^(.*[-_\\s])?(';

  const input = document.createElement('input');
  input.type = 'text';
  input.name = inputName;
  input.className = 'form-control font-monospace type-regex';

  const suffix = document.createElement('span');
  suffix.className = 'input-group-text font-monospace';
  suffix.textContent = ')([-_\\s].*)?$';

  row.append(prefix, input, suffix, buildRemoveButton());

  return row;
}

function buildWeightingRow (): HTMLDivElement
{
  const row = document.createElement('div');
  row.className = 'input-group mb-2 weighting-row';

  const assetInput = document.createElement('input');
  assetInput.type = 'text';
  assetInput.name = 'WeightingAssets';
  assetInput.className = 'form-control';
  assetInput.placeholder = 'Asset symbol';

  const weightingInput = document.createElement('input');
  weightingInput.type = 'number';
  weightingInput.name = 'WeightingValues';
  weightingInput.value = '1';
  weightingInput.className = 'form-control';
  weightingInput.min = '0';
  weightingInput.step = '0.01';
  weightingInput.placeholder = 'Weighting';

  row.append(assetInput, weightingInput, buildRemoveButton());

  return row;
}

document.querySelectorAll<HTMLElement>('.tag-row, .weighting-row').forEach(addRemoveHandler);
document.querySelectorAll<HTMLInputElement>('.type-regex').forEach(addRegexValidationHandler);

document.querySelectorAll<HTMLButtonElement>('.add-row').forEach(button =>
{
  button.addEventListener('click', () =>
  {
    const target = document.getElementById(button.dataset.target!)!;
    const { template } = button.dataset;

    const row =
      template === 'tag'
        ? buildTagRow(target.id === 'tags-to-include-rows' ? 'TagsToInclude' : 'TagsToIgnore')
        : buildWeightingRow();

    target.appendChild(row);
    addRemoveHandler(row);
    row.querySelectorAll<HTMLInputElement>('.type-regex').forEach(addRegexValidationHandler);
  });
});

document.getElementById('allocation-config-form')?.addEventListener('submit', e =>
{
  if (document.querySelectorAll('.type-regex.is-invalid').length > 0)
  {
    e.preventDefault();
  }
});