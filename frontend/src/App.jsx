import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { AuthProvider, useAuth } from './contexts/AuthContext';
import Layout from './components/layout/Layout';
import Dashboard from './pages/Dashboard';
import Login from './pages/Login';
import Products from './pages/Products';
import Categories from './pages/Categories';
import Users from './pages/Users';
import Roles from './pages/Roles';
import Warehouses from './pages/Warehouses';
import Zones from './pages/Zones';
import Locations from './pages/Locations';
import Inventory from './pages/Inventory';
import Inbound from './pages/Inbound';
import Outbound from './pages/Outbound';
import Suppliers from './pages/Suppliers';
import Carriers from './pages/Carriers';
import Transfers from './pages/Transfers';
import Returns from './pages/Returns';
import Adjustments from './pages/Adjustments';
import Picks from './pages/Picks';
import Shipments from './pages/Shipments';
import Uoms from './pages/Uoms';
import './App.css';

function ProtectedRoute({ children }) {
  const { user, loading } = useAuth();
  if (loading) return <div style={{display:'flex',justifyContent:'center',alignItems:'center',height:'100vh'}}><div className="spinner"></div></div>;
  if (!user) return <Navigate to="/login" replace />;
  return children;
}

function PublicRoute({ children }) {
  const { user, loading } = useAuth();
  if (loading) return <div style={{display:'flex',justifyContent:'center',alignItems:'center',height:'100vh'}}><div className="spinner"></div></div>;
  if (user) return <Navigate to="/" replace />;
  return children;
}

function AppRoutes() {
  return (
    <Routes>
      <Route path="/login" element={<PublicRoute><Login /></PublicRoute>} />
      <Route path="/" element={<ProtectedRoute><Layout /></ProtectedRoute>}>
        <Route index element={<Dashboard />} />
        <Route path="products" element={<Products />} />
        <Route path="categories" element={<Categories />} />
        <Route path="suppliers" element={<Suppliers />} />
        <Route path="carriers" element={<Carriers />} />
        <Route path="uoms" element={<Uoms />} />
        <Route path="warehouses" element={<Warehouses />} />
        <Route path="zones" element={<Zones />} />
        <Route path="locations" element={<Locations />} />
        <Route path="inventory" element={<Inventory />} />
        <Route path="inbound" element={<Inbound />} />
        <Route path="outbound" element={<Outbound />} />
        <Route path="picks" element={<Picks />} />
        <Route path="shipments" element={<Shipments />} />
        <Route path="transfers" element={<Transfers />} />
        <Route path="returns" element={<Returns />} />
        <Route path="adjustments" element={<Adjustments />} />
        <Route path="users" element={<Users />} />
        <Route path="roles" element={<Roles />} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}

export default function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <AppRoutes />
      </AuthProvider>
    </BrowserRouter>
  );
}
