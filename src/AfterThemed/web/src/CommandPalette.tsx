import { useEffect, useId, useRef, useState } from 'react'
import { motion } from 'framer-motion'
import { Search } from 'lucide-react'
import { duration, ease } from './motion'

export type Command = { id: string; label: string; group: string; shortcut?: string; disabled?: boolean; run: () => void }

const recentKey = 'afterthemed-recent-commands'
const readRecent = (): string[] => {
  try { return JSON.parse(localStorage.getItem(recentKey) ?? '[]') } catch { return [] }
}

/** Letters of the query appear in order in the label: "shf" finds "Shuffle unlocked colors". */
const matches = (label: string, query: string) => {
  let at = 0
  for (const char of label.toLowerCase()) if (char === query[at]) at++
  return at === query.length
}

/** Ctrl+K: every action by name, recently used ones first. */
export default function CommandPalette({ commands, onClose }: { commands: Command[]; onClose: () => void }) {
  const listId = useId()
  const input = useRef<HTMLInputElement>(null)
  const [query, setQuery] = useState('')
  const [active, setActive] = useState(0)
  const recent = readRecent()
  const available = commands.filter(command => !command.disabled)
  const results = query.trim()
    ? available.filter(command => matches(command.label, query.trim().toLowerCase()))
    : [...recent.map(id => available.find(command => command.id === id)).filter((command): command is Command => !!command)
        .map(command => ({ ...command, group: 'Recent' })),
      ...available.filter(command => !recent.includes(command.id))]

  useEffect(() => {
    const previous = document.activeElement as HTMLElement | null
    input.current?.focus()
    return () => previous?.focus()
  }, [])
  useEffect(() => setActive(0), [query])
  useEffect(() => {
    document.getElementById(`${listId}-${active}`)?.scrollIntoView({ block: 'nearest' })
  }, [active, listId])

  const run = (command: Command) => {
    try { localStorage.setItem(recentKey, JSON.stringify([command.id, ...recent.filter(id => id !== command.id)].slice(0, 4))) } catch { /* convenience only */ }
    onClose()
    command.run()
  }

  return <motion.div className="dialog-backdrop command-backdrop" initial={{ opacity: 0 }} animate={{ opacity: 1 }} transition={{ duration: duration.fast, ease: ease.enter }}
    onMouseDown={event => { if (event.target === event.currentTarget) onClose() }}>
    <motion.div className="command-palette" role="dialog" aria-modal="true" aria-label="Command center"
      initial={{ opacity: 0, y: -6 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: duration.popover, ease: ease.enter }}>
      <div className="command-search">
        <Search size={16} aria-hidden="true" />
        <input ref={input} value={query} placeholder="Type a command" spellCheck={false}
          role="combobox" aria-expanded="true" aria-controls={listId} aria-autocomplete="list"
          aria-activedescendant={results.length ? `${listId}-${active}` : undefined}
          onChange={event => setQuery(event.target.value)}
          onKeyDown={event => {
            if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
              event.preventDefault()
              setActive(index => (index + (event.key === 'ArrowDown' ? 1 : -1) + results.length) % Math.max(1, results.length))
            } else if (event.key === 'Enter' && results[active]) { event.preventDefault(); run(results[active]) }
            else if (event.key === 'Escape') { event.preventDefault(); onClose() }
            else if (event.key === 'Tab') event.preventDefault()
          }} />
        <kbd>Esc</kbd>
      </div>
      <ul id={listId} role="listbox" aria-label="Commands" className="command-list">
        {results.map((command, index) => <li key={command.id} id={`${listId}-${index}`} role="option" aria-selected={index === active} aria-label={command.label}
          data-group-start={index === 0 || results[index - 1].group !== command.group ? command.group : undefined}
          onPointerMove={() => setActive(index)} onClick={() => run(command)}>
          <span>{command.label}</span>{command.shortcut && <kbd>{command.shortcut}</kbd>}
        </li>)}
        {results.length === 0 && <li className="command-empty" role="presentation">No command matches “{query}”.</li>}
      </ul>
    </motion.div>
  </motion.div>
}
