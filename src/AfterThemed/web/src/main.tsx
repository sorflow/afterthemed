import { createRoot } from 'react-dom/client'
import '@fontsource/familjen-grotesk/latin-400.css'
import '@fontsource/familjen-grotesk/latin-500.css'
import '@fontsource/ibm-plex-mono/latin-400.css'
import '@fontsource/ibm-plex-mono/latin-500.css'
import './style.css'
import './Palette.css'
import './AePreview.css'
import './ReferenceTheme.css'
import App from './App'
import { MotionConfig } from 'framer-motion'
import './StudioFinish.css'
import './ReferenceBlue.css'
import './InstallThemeButton.css'
import './Features.css'
import './Fall.css'
import './Distinct.css'

createRoot(document.getElementById('root')!).render(
  <MotionConfig reducedMotion="user"><App /></MotionConfig>,
)
