import {
  LayoutDashboard, Package, Users, Shield, Warehouse,
  ArrowRightLeft, Truck, FileText,
  Settings, LogOut, Menu, X, ChevronDown
} from 'lucide-react';
import { useState } from 'react';
import { NavLink, useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';
import './Sidebar.css';

const menuItems = [
  { label: 'Dashboard', path: '/', icon: LayoutDashboard, permission: 'read' },
  { 
    label: 'Master Data', 
    icon: Package, 
    permission: 'read',
    children: [
      { label: 'Products', path: '/products', icon: Package },
      { label: 'Categories', path: '/categories', icon: FileText },
      { label: 'Suppliers', path: '/suppliers', icon: Truck },
      { label: 'Carriers', path: '/carriers', icon: Truck },
      { label: 'UOMs', path: '/uoms', icon: Settings },
    ]
  },
  { 
    label: 'Warehouse', 
    icon: Warehouse, 
    permission: 'read',
    children: [
      { label: 'Warehouses', path: '/warehouses', icon: Warehouse },
      { label: 'Zones', path: '/zones', icon: Package },
      { label: 'Locations (Bins)', path: '/locations', icon: Package },
    ]
  },
  { label: 'Inventory', path: '/inventory', icon: Package, permission: 'read' },
  { 
    label: 'Inbound', 
    icon: ArrowRightLeft, 
    permission: 'read',
    children: [
      { label: 'Receipts', path: '/inbound', icon: ArrowRightLeft },
    ]
  },
  { 
    label: 'Outbound', 
    icon: ArrowRightLeft, 
    permission: 'read',
    children: [
      { label: 'Orders', path: '/outbound', icon: ArrowRightLeft },
      { label: 'Pick Tasks', path: '/picks', icon: Package },
      { label: 'Shipments', path: '/shipments', icon: Truck },
    ]
  },
  { label: 'Transfers', path: '/transfers', icon: ArrowRightLeft, permission: 'read' },
  { label: 'Returns', path: '/returns', icon: ArrowRightLeft, permission: 'read' },
  { label: 'Adjustments', path: '/adjustments', icon: Settings, permission: 'read' },
  { divider: true },
  { label: 'Users', path: '/users', icon: Users, permission: 'manage' },
  { label: 'Roles', path: '/roles', icon: Shield, permission: 'admin' },
];

export default function Sidebar() {
  const [open, setOpen] = useState(true);
  const location = useLocation();
  // Workflow groups start open; a group also opens when the current page lives inside it
  const [expandedMenus, setExpandedMenus] = useState({ Inbound: true, Outbound: true });
  const { user, logout, can } = useAuth();
  const navigate = useNavigate();

  function handleLogout() {
    logout();
    navigate('/login');
  }

  function toggleMenu(label) {
    setExpandedMenus(prev => ({ ...prev, [label]: !prev[label] }));
  }

  function renderMenuItem(item) {
    if (item.permission && !can(item.permission)) return null;

    if (item.divider) return <hr key="div" className="sidebar-divider" />;

    if (item.children) {
      const isExpanded = expandedMenus[item.label] || item.children.some(c => c.path === location.pathname);
      const Icon = item.icon;
      return (
        <div key={item.label} className="menu-group">
          <button className={`menu-btn ${isExpanded ? 'active' : ''}`} onClick={() => toggleMenu(item.label)}>
            <Icon size={18} />
            {open && <span>{item.label}</span>}
            {open && <ChevronDown size={14} className={`chevron ${isExpanded ? 'rotated' : ''}`} />}
          </button>
          {open && isExpanded && (
            <div className="submenu">
              {item.children.map(child => {
                const ChildIcon = child.icon;
                return (
                  <NavLink key={child.path} to={child.path} className={({ isActive }) => `submenu-item ${isActive ? 'active' : ''}`}>
                    <ChildIcon size={15} />
                    <span>{child.label}</span>
                  </NavLink>
                );
              })}
            </div>
          )}
        </div>
      );
    }

    const Icon = item.icon;
    if (item.path) {
      return (
        <NavLink key={item.path} to={item.path} className={({ isActive }) => `menu-btn ${isActive ? 'active' : ''}`}>
          <Icon size={18} />
          {open && <span>{item.label}</span>}
        </NavLink>
      );
    }

    return null;
  }

  return (
    <aside className={`sidebar ${open ? 'open' : 'closed'}`}>
      <div className="sidebar-header">
        {open && (
          <div className="logo">
            <Warehouse size={24} color="#38bdf8" />
            <span>WMS Distribution Center</span>
          </div>
        )}
        <button className="toggle-btn" onClick={() => setOpen(!open)}>
          {open ? <X size={18} /> : <Menu size={18} />}
        </button>
      </div>

      <nav className="sidebar-nav">
        {menuItems.map(renderMenuItem)}
      </nav>

      {open && user && (
        <div className="sidebar-footer">
          <div className="user-info">
            <div className="user-avatar">{user.fullName?.charAt(0) || 'U'}</div>
            <div className="user-details">
              <span className="user-name">{user.fullName || user.username}</span>
              <span className="user-role">{user.role}</span>
            </div>
          </div>
          <button className="logout-btn" onClick={handleLogout}>
            <LogOut size={16} />
            <span>Logout</span>
          </button>
        </div>
      )}
    </aside>
  );
}
