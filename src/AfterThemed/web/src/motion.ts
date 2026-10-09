// The motion vocabulary: few durations, three curves, no springs. CSS uses the same values as
// --motion-* and --ease-* custom properties (Distinct.css).
export const ease = {
  enter: [0, 0, .4, 1] as const,
  exit: [.5, 0, 1, 1] as const,
  move: [.45, 0, .4, 1] as const,
}

export const duration = {
  press: .1,
  fast: .14,
  popover: .18,
  row: .2,
  inspector: .22,
  panel: .26,
  signature: .38,
}

/** Leaving is a little faster than arriving and uses the exit curve. */
export const leave = (seconds: number) => ({ duration: seconds * .75, ease: ease.exit })
