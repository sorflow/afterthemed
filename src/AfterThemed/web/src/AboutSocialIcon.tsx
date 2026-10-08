type Platform = 'x' | 'youtube' | 'instagram' | 'discord'

// Local SVGs keep the social marks available offline in the desktop app.
export default function AboutSocialIcon({ platform }: { platform: Platform }) {
  return <svg className="about-social-icon" viewBox="0 0 24 24" width="20" height="20" fill="currentColor" aria-hidden="true" focusable="false">
    {platform === 'x' && <path d="M18.9 2H22l-6.8 7.8L23.2 22h-6.3L12 14.6 5.5 22H2.3l7.3-8.5L1.8 2h6.5l4.5 6.8L18.9 2Zm-1.1 18h1.7L7.3 3.9H5.5L17.8 20Z" />}
    {platform === 'youtube' && <><path d="M23.5 6.2a3 3 0 0 0-2.1-2.1C19.5 3.6 12 3.6 12 3.6s-7.5 0-9.4.5A3 3 0 0 0 .5 6.2 31 31 0 0 0 0 12a31 31 0 0 0 .5 5.8 3 3 0 0 0 2.1 2.1c1.9.5 9.4.5 9.4.5s7.5 0 9.4-.5a3 3 0 0 0 2.1-2.1A31 31 0 0 0 24 12a31 31 0 0 0-.5-5.8Z" /><path d="m9.6 15.6 6.2-3.6-6.2-3.6Z" fill="var(--ui-raised)" /></>}
    {platform === 'instagram' && <><rect x="3" y="3" width="18" height="18" rx="5" fill="none" stroke="currentColor" strokeWidth="2" /><circle cx="12" cy="12" r="4" fill="none" stroke="currentColor" strokeWidth="2" /><circle cx="17.5" cy="6.5" r="1.2" /></>}
    {platform === 'discord' && <path d="M20.3 4.4a20 20 0 0 0-4.9-1.5l-.6 1.2a18 18 0 0 0-5.6 0l-.6-1.2a20 20 0 0 0-4.9 1.5C.6 9 .1 13.5.4 17.9a20 20 0 0 0 6 3l1.3-2.1-1.9-.9.5-.4a14 14 0 0 0 11.4 0l.5.4-1.9.9 1.3 2.1a20 20 0 0 0 6-3c.4-5.1-.8-9.5-3.3-13.5ZM8.2 15.2c-1.1 0-1.9-1-1.9-2.2s.8-2.2 1.9-2.2 1.9 1 1.9 2.2-.8 2.2-1.9 2.2Zm7.6 0c-1.1 0-1.9-1-1.9-2.2s.8-2.2 1.9-2.2 1.9 1 1.9 2.2-.8 2.2-1.9 2.2Z" />}
  </svg>
}
