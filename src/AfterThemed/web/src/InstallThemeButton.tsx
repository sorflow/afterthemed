import { motion } from 'framer-motion'
import { Check, AlertCircle } from 'lucide-react'
import PixelField from './PixelField'
import { duration, ease, leave } from './motion'

export type InstallStatus = 'idle' | 'preparing' | 'installing' | 'installed' | 'failed' | 'cancelled'
const labels: Record<InstallStatus, string> = { idle: 'Install theme', preparing: 'Preparing…', installing: 'Installing…', installed: 'Installed', failed: 'Try again', cancelled: 'Install theme' }

export default function InstallThemeButton({ onInstall, status = 'idle', detail = '', colors }: {
  onInstall: () => void; status?: InstallStatus; detail?: string; colors?: string[]
}) {
  const busy = status === 'preparing' || status === 'installing'
  return <>
    <motion.button type="button" className="primary-button install-theme-button pixel-install-button"
      data-status={status} disabled={busy} aria-busy={busy} aria-describedby="install-status"
      title={detail || labels[status]} onClick={onInstall} whileTap={busy ? undefined : { scale: .98 }} transition={{ duration: duration.press, ease: ease.move }}>
      <PixelField colors={colors} />
      <span className="install-button-content">
        {status === 'installed' ? <Check size={14} aria-hidden="true" /> : status === 'failed' ? <AlertCircle size={14} aria-hidden="true" /> : null}
        <span className="install-theme-label">{labels[status]}</span>
      </span>
    </motion.button>
    <span id="install-status" className="action-status-announcement" role="status" aria-live="polite">{detail}</span>
  </>
}

export type InstallStage = '' | 'original' | 'generate' | 'install' | 'done'
const stages = [['original', 'Preserve original'], ['generate', 'Generate theme'], ['install', 'Install & verify']] as const

/**
 * The install as the engine runs it: each stage's keyframe fills as it completes. Visual only; the
 * button's live region already announces the same progress.
 */
export function InstallRail({ status = 'idle', stage = '', onDismiss }: {
  status?: InstallStatus; stage?: InstallStage; onDismiss: () => void
}) {
  const reached = stage === 'done' ? stages.length : stages.findIndex(([key]) => key === stage)
  return <motion.div className="install-rail" data-status={status} aria-hidden="true" onClick={onDismiss}
    initial={{ opacity: 0, y: -4 }} animate={{ opacity: 1, y: 0 }} exit={{ opacity: 0, transition: leave(duration.popover) }}
    transition={{ duration: duration.popover, ease: ease.enter }}>
    <ol>
      {stages.map(([key, label], index) => <li key={key}
        data-state={index < reached ? 'done' : index === reached ? (status === 'failed' ? 'failed' : 'current') : 'pending'}>
        <i /><span>{label}</span>
      </li>)}
    </ol>
    <strong>{status === 'installed' ? 'Theme installed' : status === 'failed' ? 'Install stopped' : 'Installing…'}</strong>
  </motion.div>
}
