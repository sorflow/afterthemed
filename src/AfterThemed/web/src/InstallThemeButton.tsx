import { motion } from 'framer-motion'
import { Check, AlertCircle } from 'lucide-react'
import PixelField from './PixelField'

export type InstallStatus = 'idle' | 'preparing' | 'installing' | 'installed' | 'failed' | 'cancelled'
const labels: Record<InstallStatus, string> = { idle: 'Install theme', preparing: 'Preparing…', installing: 'Installing…', installed: 'Installed', failed: 'Try again', cancelled: 'Install theme' }

export default function InstallThemeButton({ onInstall, status = 'idle', detail = '', colors }: {
  onInstall: () => void; status?: InstallStatus; detail?: string; colors?: string[]
}) {
  const busy = status === 'preparing' || status === 'installing'
  return <>
    <motion.button type="button" className="primary-button install-theme-button pixel-install-button"
      data-status={status} disabled={busy} aria-busy={busy} aria-describedby="install-status"
      title={detail || labels[status]} onClick={onInstall} whileTap={busy ? undefined : { scale: 0.98 }} transition={{ duration: 0.16 }}>
      <PixelField colors={colors} />
      <span className="install-button-content">
        {status === 'installed' ? <Check size={14} aria-hidden="true" /> : status === 'failed' ? <AlertCircle size={14} aria-hidden="true" /> : null}
        <span className="install-theme-label">{labels[status]}</span>
      </span>
    </motion.button>
    <span id="install-status" className="action-status-announcement" role="status" aria-live="polite">{detail}</span>
  </>
}
