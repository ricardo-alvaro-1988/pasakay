import { ReactNode, useEffect, useMemo, useState } from 'react'
import type { PageId } from './api'

export type SideNavItem = {
  id: PageId
  label: string
  icon: string
  live?: boolean
  pending?: boolean
}

export type SideNavGroup = {
  id: string
  label: string
  items: SideNavItem[]
}

const GROUP_STATE_KEY = 'yapasakay-nav-groups-v2'

function readGroupState(scope: string): Record<string, boolean> {
  try {
    const raw = localStorage.getItem(`${GROUP_STATE_KEY}:${scope}`)
    if (!raw) return {}
    const parsed = JSON.parse(raw) as Record<string, boolean>
    return parsed && typeof parsed === 'object' ? parsed : {}
  } catch {
    return {}
  }
}

function writeGroupState(scope: string, state: Record<string, boolean>) {
  localStorage.setItem(`${GROUP_STATE_KEY}:${scope}`, JSON.stringify(state))
}

export function filterMenuGroups(groups: SideNavGroup[], allowedIds: Set<PageId> | null): SideNavGroup[] {
  return groups
    .map((group) => ({
      ...group,
      items: allowedIds
        ? group.items.filter((item) => allowedIds.has(item.id))
        : group.items,
    }))
    .filter((group) => group.items.length > 0)
}

export function flattenMenuGroups(groups: SideNavGroup[]): SideNavItem[] {
  return groups.flatMap((group) => group.items)
}

export function SideNav({
  scope,
  groups,
  page,
  shellCollapsed,
  onNavigate,
  badge,
}: {
  scope: string
  groups: SideNavGroup[]
  page: PageId
  shellCollapsed: boolean
  onNavigate: (id: PageId) => void
  badge?: (id: PageId) => ReactNode
}) {
  const [openGroups, setOpenGroups] = useState<Record<string, boolean>>(() => readGroupState(scope))

  useEffect(() => {
    writeGroupState(scope, openGroups)
  }, [scope, openGroups])

  const activeGroupId = useMemo(
    () => groups.find((group) => group.items.some((item) => item.id === page))?.id ?? null,
    [groups, page],
  )

  useEffect(() => {
    if (!activeGroupId) return
    setOpenGroups((current) => {
      if (current[activeGroupId] === true) return current
      return { ...current, [activeGroupId]: true }
    })
  }, [page, activeGroupId])

  function isOpen(groupId: string) {
    if (openGroups[groupId] !== undefined) {
      return openGroups[groupId]
    }
    return groupId === activeGroupId
  }

  function toggleGroup(groupId: string) {
    setOpenGroups((current) => ({
      ...current,
      [groupId]: !isOpen(groupId),
    }))
  }

  if (shellCollapsed) {
    return (
      <nav className="nav nav-compact">
        {groups.flatMap((group) => group.items).map((item) => (
          <button
            key={item.id}
            type="button"
            className={`${page === item.id ? 'active' : ''}${item.pending ? ' pending' : ''}`}
            title={item.pending ? `${item.label} (pending)` : item.label}
            onClick={() => onNavigate(item.id)}
          >
            <span className="ico">{item.icon}</span>
            <span className="label">{item.label}</span>
            {badge?.(item.id)}
          </button>
        ))}
      </nav>
    )
  }

  return (
    <nav className="nav nav-compact nav-grouped">
      {groups.map((group) => {
        const open = isOpen(group.id)
        const hasActive = group.items.some((item) => item.id === page)
        return (
          <div key={group.id} className={`nav-group${open ? ' open' : ''}${hasActive ? ' has-active' : ''}`}>
            <button
              type="button"
              className="nav-group-toggle"
              aria-expanded={open}
              onClick={() => toggleGroup(group.id)}
            >
              <span className="nav-group-label">{group.label}</span>
              <span className="nav-group-chevron" aria-hidden="true">{open ? '▾' : '▸'}</span>
            </button>
            {open ? (
              <div className="nav-group-items">
                {group.items.map((item) => (
                  <button
                    key={item.id}
                    type="button"
                    className={`${page === item.id ? 'active' : ''}${item.pending ? ' pending' : ''}`}
                    title={item.pending ? `${item.label} (pending)` : item.label}
                    onClick={() => onNavigate(item.id)}
                  >
                    <span className="ico">{item.icon}</span>
                    <span className="label">
                      {item.label}
                      {item.pending ? <span className="nav-pending">pending</span> : null}
                    </span>
                    {badge?.(item.id)}
                  </button>
                ))}
              </div>
            ) : null}
          </div>
        )
      })}
    </nav>
  )
}

export const ADMIN_MENU_GROUPS: SideNavGroup[] = [
  {
    id: 'main',
    label: 'Main',
    items: [{ id: 'overview', label: 'Overview', icon: '⌂', live: true }],
  },
  {
    id: 'network',
    label: 'Network',
    items: [
      { id: 'operators', label: 'Operators', icon: '▦', live: true },
      { id: 'customers', label: 'Customers', icon: '☺', live: true },
      { id: 'territories', label: 'Territories', icon: '◎', live: true },
    ],
  },
  {
    id: 'finance',
    label: 'Finance',
    items: [
      { id: 'fares', label: 'Fare matrix', icon: '₱', live: true },
      { id: 'billing', label: 'Billing', icon: '▤', live: true },
    ],
  },
  {
    id: 'report',
    label: 'Report',
    items: [
      { id: 'commission', label: 'Commission', icon: '％', live: true },
    ],
  },
  {
    id: 'comms',
    label: 'Comms',
    items: [
      { id: 'announcements', label: 'Announcements', icon: '✺', live: true },
      { id: 'support', label: 'Support', icon: '☎', live: true },
    ],
  },
  {
    id: 'access',
    label: 'Access',
    items: [
      { id: 'roles', label: 'Roles', icon: '◉', live: true },
      { id: 'admins', label: 'Admin users', icon: '★', live: true },
    ],
  },
  {
    id: 'system',
    label: 'System',
    items: [
      { id: 'audit', label: 'Audit', icon: '☰', live: true },
      { id: 'settings', label: 'Settings', icon: '⚙', live: true },
    ],
  },
]

export const OPERATOR_MENU_GROUPS: SideNavGroup[] = [
  {
    id: 'home',
    label: 'Home',
    items: [
      { id: 'dashboard', label: 'Dashboard', icon: '◎' },
      { id: 'overview', label: 'Overview', icon: '⌂' },
    ],
  },
  {
    id: 'trips',
    label: 'Trips',
    items: [
      { id: 'bookings', label: 'Booking', icon: '▢' },
      { id: 'schedule', label: 'Schedule', icon: '◷' },
    ],
  },
  {
    id: 'people',
    label: 'People',
    items: [
      { id: 'riders', label: 'Riders', icon: '▣' },
      { id: 'customers', label: 'Customers', icon: '☺' },
      { id: 'fleet', label: 'Fleet', icon: '⌖' },
    ],
  },
  {
    id: 'rates',
    label: 'Rates',
    items: [
      { id: 'fares', label: 'Fare matrix', icon: '₱' },
      { id: 'derive-fares', label: 'Derive fare', icon: '◎' },
      { id: 'surcharges', label: 'Surcharges', icon: '+' },
    ],
  },
  {
    id: 'support',
    label: 'Support',
    items: [
      { id: 'support', label: 'Support', icon: '☎' },
      { id: 'inbox', label: 'Inbox', icon: '✉' },
    ],
  },
  {
    id: 'pabili',
    label: 'Pabili',
    items: [
      { id: 'pabili-orders', label: 'Order', icon: '▢' },
      { id: 'merchants', label: 'Merchant', icon: '◇' },
      { id: 'pabili-ads', label: 'Exclusive Offer', icon: '★' },
      { id: 'product-categories', label: 'Product Categories', icon: '☰' },
      { id: 'pabili-matrix', label: 'Pabili Matrix', icon: '₱' },
      { id: 'pabili-riders', label: 'Rider', icon: '▣' },
      { id: 'pabili-customers', label: 'Customer', icon: '☺', pending: true },
      { id: 'pabili-surcharges', label: 'Surcharge', icon: '+' },
      { id: 'pabili-commission', label: 'Commission Report', icon: '％', pending: true },
    ],
  },
  {
    id: 'money',
    label: 'Money',
    items: [
      { id: 'billing', label: 'Billing', icon: '▤' },
      { id: 'wallet', label: 'Wallet', icon: '◈' },
      { id: 'promos', label: 'Promos', icon: '%' },
    ],
  },
  {
    id: 'report',
    label: 'Report',
    items: [
      { id: 'commission', label: 'Commission', icon: '％' },
      { id: 'booking-report', label: 'Booking', icon: '☰' },
      { id: 'rider-report', label: 'Rider', icon: '♟' },
      { id: 'customer-report', label: 'Customer', icon: '☺' },
    ],
  },
  {
    id: 'company',
    label: 'Company',
    items: [
      { id: 'company', label: 'Company', icon: '▦' },
      { id: 'roles', label: 'Roles', icon: '◉' },
      { id: 'employees', label: 'Employees', icon: '♟' },
    ],
  },
]
