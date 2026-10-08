import { createRoot } from 'react-dom/client'
import { FluentProvider, webDarkTheme } from '@fluentui/react-components'
import { SolarProvider } from '@solar-icons/react'
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
  <FluentProvider theme={webDarkTheme}>
    <SolarProvider size={18} color="currentColor" strokeWidth={1.8}>
      <MotionConfig reducedMotion="user"><App /></MotionConfig>
    </SolarProvider>
  </FluentProvider>,
)
