import { useId, useLayoutEffect, useRef, useState, type KeyboardEvent } from 'react'
import { createPortal } from 'react-dom'
import { Check, ChevronDown } from 'lucide-react'

type Option = { value: string; label: string }

export default function Dropdown({ id, label, value, options, onChange }: {
  id: string; label: string; value: string; options: Option[]; onChange: (value: string) => void
}) {
  const listId = useId()
  const trigger = useRef<HTMLButtonElement>(null)
  const menu = useRef<HTMLDivElement>(null)
  const [open, setOpen] = useState(false)
  const [active, setActive] = useState(0)
  const [bounds, setBounds] = useState({ left: 0, top: 0, width: 0, height: 320 })
  const search = useRef({ text: '', time: 0 })
  const selected = options.findIndex(option => option.value === value)

  const close = () => { setOpen(false); trigger.current?.focus() }
  const show = () => { setActive(Math.max(0, selected)); setOpen(true) }
  useLayoutEffect(() => {
    if (!open) return
    const measure = () => {
      const rect = trigger.current?.getBoundingClientRect()
      if (!rect) return
      const below = window.innerHeight - rect.bottom - 16
      const above = rect.top - 16
      const height = Math.max(40, Math.min(320, Math.max(below, above)))
      const width = Math.min(Math.max(rect.width, 240), window.innerWidth - 24)
      setBounds({ width, height, left: Math.max(12, Math.min(rect.left, window.innerWidth - width - 12)), top: below >= height ? rect.bottom + 6 : Math.max(12, rect.top - height - 6) })
    }
    const outside = (event: PointerEvent) => {
      if (!trigger.current?.contains(event.target as Node) && !menu.current?.contains(event.target as Node)) setOpen(false)
    }
    measure()
    menu.current?.focus()
    document.addEventListener('pointerdown', outside)
    window.addEventListener('resize', measure)
    window.addEventListener('scroll', measure, true)
    return () => {
      document.removeEventListener('pointerdown', outside)
      window.removeEventListener('resize', measure)
      window.removeEventListener('scroll', measure, true)
    }
  }, [open])

  useLayoutEffect(() => {
    const list = menu.current
    const row = list?.querySelector<HTMLElement>(`[data-index="${active}"]`)
    if (!open || !list || !row) return
    const top = row.offsetTop
    const bottom = top + row.offsetHeight
    if (top < list.scrollTop) list.scrollTop = top
    else if (bottom > list.scrollTop + list.clientHeight) list.scrollTop = bottom - list.clientHeight
  }, [active, open, bounds.height])

  const keys = (event: KeyboardEvent) => {
    if (event.key === 'Escape') { event.preventDefault(); close(); return }
    if (event.key === 'Tab') { setOpen(false); trigger.current?.focus(); return }
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault()
      if (options[active]) onChange(options[active].value)
      close()
      return
    }
    let next = active
    if (event.key === 'ArrowDown') next = Math.min(options.length - 1, active + 1)
    else if (event.key === 'ArrowUp') next = Math.max(0, active - 1)
    else if (event.key === 'Home') next = 0
    else if (event.key === 'End') next = options.length - 1
    else if (event.key.length === 1 && !event.ctrlKey && !event.metaKey && !event.altKey) {
      const now = Date.now()
      search.current = { text: (now - search.current.time < 700 ? search.current.text : '') + event.key.toLowerCase(), time: now }
      const match = options.findIndex(option => option.label.toLowerCase().startsWith(search.current.text))
      if (match >= 0) next = match
    } else return
    event.preventDefault()
    setActive(next)
  }

  return <>
    <button ref={trigger} id={id} type="button" className="editor-dropdown" aria-label={label} aria-haspopup="listbox" aria-expanded={open} aria-controls={open ? listId : undefined}
      onClick={() => open ? close() : show()} onKeyDown={event => {
        if (['ArrowDown', 'ArrowUp', 'Enter', ' '].includes(event.key)) { event.preventDefault(); show() }
      }}><span>{options[selected]?.label ?? 'Choose an option'}</span><ChevronDown size={15} className={open ? 'is-open' : ''} /></button>
    {open && createPortal(<div ref={menu} id={listId} role="listbox" aria-label={label} aria-activedescendant={`${listId}-${active}`} tabIndex={-1} className="editor-dropdown-menu" style={{ left: bounds.left, top: bounds.top, width: bounds.width, maxHeight: bounds.height }} onKeyDown={keys}>
      {options.map((option, index) => <div key={option.value} id={`${listId}-${index}`} role="option" aria-selected={option.value === value} data-index={index}
        className={`editor-dropdown-option${active === index ? ' active' : ''}`} onPointerMove={() => setActive(index)} onClick={() => { onChange(option.value); close() }}>
        <span>{option.label}</span>{option.value === value && <Check size={16} />}
      </div>)}
    </div>, document.body)}
  </>
}
