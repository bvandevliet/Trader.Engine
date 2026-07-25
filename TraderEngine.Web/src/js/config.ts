function addRemoveHandler (row: HTMLElement): void
{
  row.querySelector<HTMLButtonElement>('.remove-row')?.addEventListener('click', () => row.remove());
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
  input.className = 'form-control font-monospace';

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
  });
});