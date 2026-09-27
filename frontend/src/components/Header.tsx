import { useState } from 'react';

type NavItem = 'dashboard' | 'repository-analysis' | 'code-review' | 'settings';

interface HeaderProps {
  activeNav: NavItem;
  onNavChange: (item: NavItem) => void;
}

const NAV_ITEMS: { id: NavItem; label: string }[] = [
  { id: 'dashboard', label: 'Dashboard' },
  { id: 'repository-analysis', label: 'Repository Analysis' },
  { id: 'code-review', label: 'Code Review' },
  { id: 'settings', label: 'Settings' },
];

export function Header({ activeNav, onNavChange }: HeaderProps) {
  const [menuOpen, setMenuOpen] = useState(false);

  return (
    <header className="app-header">
      <div className="header-inner">
        <div className="header-brand">
          <div className="brand-icon" aria-hidden="true">
            <svg width="22" height="22" viewBox="0 0 22 22" fill="none">
              <rect width="22" height="22" rx="6" fill="#6366f1" />
              <path
                d="M6 14l4-6 3 4 2-2.5 3 4.5"
                stroke="#fff"
                strokeWidth="1.8"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
            </svg>
          </div>
          <span className="brand-name">DevFlow AI</span>
        </div>

        <nav className={`header-nav${menuOpen ? ' header-nav--open' : ''}`} aria-label="Main navigation">
          {NAV_ITEMS.map((item) => (
            <button
              key={item.id}
              className={`nav-item${activeNav === item.id ? ' nav-item--active' : ''}`}
              onClick={() => {
                onNavChange(item.id);
                setMenuOpen(false);
              }}
            >
              {item.label}
            </button>
          ))}
        </nav>

        <button
          className="menu-toggle"
          aria-label="Toggle navigation"
          aria-expanded={menuOpen}
          onClick={() => setMenuOpen((v) => !v)}
        >
          <span />
          <span />
          <span />
        </button>
      </div>
    </header>
  );
}
