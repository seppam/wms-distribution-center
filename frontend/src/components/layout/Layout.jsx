import { Outlet } from 'react-router-dom';
import DemoBanner from './DemoBanner';
import Sidebar from './Sidebar';
import './Layout.css';

export default function Layout() {
  return (
    <div className="app-layout">
      <Sidebar />
      <main className="main-content">
        <DemoBanner />
        <Outlet />
      </main>
    </div>
  );
}
