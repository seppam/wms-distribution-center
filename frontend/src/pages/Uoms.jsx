import CrudPage from '../components/ui/CrudPage';
import { useOptions } from '../hooks/useOptions';
import { uomsApi } from '../services';

const columns = [
  { key: 'code', label: 'Code', render: (v) => <span className="code-chip">{v}</span> },
  { key: 'name', label: 'Name' },
  { key: 'baseUomName', label: 'Base unit' },
  { key: 'conversionFactor', label: 'Conversion factor' },
  { key: 'isBase', label: 'Base', render: (v) => (v ? <span className="badge badge-info">Base</span> : '-') },
];

export default function Uoms() {
  const bases = useOptions(() => uomsApi.getAll(), (u) => `${u.code} - ${u.name}`);
  const formFields = [
    { name: 'code', label: 'Code *', type: 'text', required: true, placeholder: 'e.g. CTN', createOnly: true },
    { name: 'name', label: 'Name *', type: 'text', required: true },
    { name: 'baseUomId', label: 'Base unit', type: 'select', options: bases.options },
    { name: 'conversionFactor', label: 'Conversion factor to base *', type: 'number', required: true, step: '0.0001', min: 0.0001 },
    { name: 'isBase', label: 'Is base unit', type: 'checkbox' },
  ];
  return (
    <CrudPage title="Units of Measure" singular="Unit" subtitle="UOM definitions with conversion factors (PCS, CTN, PLT)"
      apiService={uomsApi} columns={columns} formFields={formFields} initialValues={{ conversionFactor: 1, isBase: false }} />
  );
}
