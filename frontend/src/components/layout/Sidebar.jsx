import {
  LayoutDashboard, Package, Users, Shield, Warehouse,
  ArrowRightLeft, Truck, FileText,
  Settings, LogOut, Menu, X, ChevronDown
} from 'lucide-react';
import { useState } from 'react';
import { NavLink, useNavigate } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';
import './Sidebar.css';

const menuItems = [
  { label: 'Dashboard', path: '/', icon: LayoutDashboard, permission: null },
  { 
    label: 'Master Data', 
    icon: Package, 
    permission: null,
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
    permission: null,
    children: [
      { label: 'Warehouses', path: '/warehouses', icon: Warehouse },
      { label: 'Zones', path: '/zones', icon: Package },
      { label: 'Locations (Bins)', path: '/locations', icon: Package },
    ]
  },
  { label: 'Inventory', path: '/inventory', icon: Package, permission: 'inventory' },
  { 
    label: 'Inbound', 
    icon: ArrowRightLeft, 
    permission: 'inbound',
    children: [
      { label: 'Receipts', path: '/inbound', icon: ArrowRightLeft },
    ]
  },
  { 
    label: 'Outbound', 
    icon: ArrowRightLeft, 
    permission: 'outbound',
    children: [
      { label: 'Orders', path: '/outbound', icon: ArrowRightLeft },
      { label: 'Pick Tasks', path: '/picks', icon: Package },
      { label: 'Shipments', path: '/shipments', icon: Truck },
    ]
  },
  { label: 'Transfers', path: '/transfers', icon: ArrowRightLeft, permission: 'transfers' },
  { label: 'Returns', path: '/returns', icon: ArrowRightLeft, permission: 'returns' },
  { label: 'Adjustments', path: '/adjustments', icon: Settings, permission: 'inventory' },
  { divider: true },
  { label: 'Users', path: '/users', icon: Users, permission: 'users' },
  { label: 'Roles', path: '/roles', icon: Shield, permission: 'roles' },
];

export default function Sidebar() {
  const [open, setOpen] = useState(true);
  const [expandedMenus, setExpandedMenus] = useState({});
  const { user, logout, isAdmin } = useAuth();
  const navigate = useNavigate();

  function handleLogout() {
    logout();
    navigate('/login');
  }

  function toggleMenu(label) {
    setExpandedMenus(prev => ({ ...prev, [label]: !prev[label] }));
  }

  function renderMenuItem(item) {
    // Permission check
    if (item.permission && !isAdmin) {
      // In real app, check hasPermission here
    }

    if (item.divider) return <hr key="div" className="sidebar-divider" />;

    if (item.children) {
      const isExpanded = expandedMenus[item.label];
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
              <span className="user-role">{user.role?.name}</span>
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
