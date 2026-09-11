// Decorative field behind the sign-in card. Every glyph is drawn on the same
// 24-unit grid and scaled with non-scaling strokes, so the layer reads as one
// icon set at one weight rather than clip art. Hidden from assistive tech.
type Mark = {
  id: string;
  x: number;
  y: number;
  size: number;
  rotate?: number;
  opacity: number;
};

const marks: Mark[] = [
  { id: "aircraft", x: 118, y: 150, size: 190, rotate: -18, opacity: 0.1 },
  { id: "tower", x: 975, y: 96, size: 128, opacity: 0.08 },
  { id: "radar", x: 812, y: 545, size: 224, opacity: 0.07 },
  { id: "gauge", x: 214, y: 592, size: 140, opacity: 0.08 },
  { id: "shield", x: 566, y: 232, size: 104, opacity: 0.06 },
  { id: "route", x: 372, y: 392, size: 168, opacity: 0.07 },
  { id: "wrench", x: 1064, y: 350, size: 100, rotate: 12, opacity: 0.07 },
  { id: "clipboard", x: 150, y: 372, size: 92, rotate: -8, opacity: 0.06 },
  { id: "aircraft", x: 900, y: 706, size: 132, rotate: 26, opacity: 0.07 },
  { id: "cloud", x: 604, y: 712, size: 112, opacity: 0.06 },
  { id: "tower", x: 430, y: 74, size: 80, opacity: 0.06 },
  { id: "gauge", x: 1116, y: 620, size: 88, opacity: 0.06 },
  { id: "clipboard", x: 742, y: 128, size: 78, rotate: 6, opacity: 0.05 },
  { id: "cloud", x: 62, y: 466, size: 90, opacity: 0.05 },
];

export function AviationBackdrop() {
  return (
    <div className="login-backdrop" aria-hidden="true">
      <svg
        className="login-backdrop-icons"
        viewBox="0 0 1200 800"
        preserveAspectRatio="xMidYMid slice"
        xmlns="http://www.w3.org/2000/svg"
      >
        <defs>
          <g id="aircraft">
            <path d="M12 2c1.1 0 2 1.6 2 3.6v3.2l7.5 4.3v2.4L14 13.3v4.4l2.6 1.8v1.9L12 20l-4.6 1.4v-1.9L10 17.7v-4.4l-7.5 2.2v-2.4L10 8.8V5.6C10 3.6 10.9 2 12 2Z" />
          </g>
          <g id="tower">
            <path d="M9 21h6M10.5 21 9.8 9m3.7 12 .7-12M8 9h8M9.2 5.2 12 3l2.8 2.2M12 3v6M5 7.5C5 5.4 6 3.6 7.4 2.5M19 7.5c0-2.1-1-3.9-2.4-5" />
          </g>
          <g id="radar">
            <circle cx="12" cy="12" r="9.2" />
            <circle cx="12" cy="12" r="5.6" />
            <circle cx="12" cy="12" r="1.9" />
            <path d="M12 2.8v18.4M2.8 12h18.4M18.5 5.5 5.5 18.5" />
          </g>
          <g id="gauge">
            <path d="M3.4 16.4a9 9 0 1 1 17.2 0" />
            <path d="M12 16.5 16.6 9.6" />
            <circle cx="12" cy="16.6" r="1.5" />
            <path d="M5.6 11.2 6.8 12M12 6.8V8M18.4 11.2 17.2 12" />
          </g>
          <g id="shield">
            <path d="M12 2.7 4.6 5.6v6.1c0 4.6 3.1 8 7.4 9.6 4.3-1.6 7.4-5 7.4-9.6V5.6Z" />
            <path d="m8.7 11.9 2.4 2.4 4.2-4.6" />
          </g>
          <g id="route">
            <circle cx="4.6" cy="18.4" r="2.1" />
            <circle cx="19.4" cy="5.6" r="2.1" />
            <path d="M6.4 16.8c3.4-2.6 3.1-6 6.2-7.6 1.6-.8 3.4-1.2 5.2-2.2" />
            <path d="M11.5 17.8h7.9M11.5 13.4h4.2" />
          </g>
          <g id="wrench">
            <path d="M15.6 3.4a5.2 5.2 0 0 0-4.3 8.1L3.6 19.2a1.7 1.7 0 0 0 2.4 2.4l7.7-7.7a5.2 5.2 0 0 0 6.6-6.9l-3 3-2.8-.7-.7-2.8 2.9-3a5.2 5.2 0 0 0-1.1-.1Z" />
          </g>
          <g id="clipboard">
            <path d="M9 4.2H7.2A1.7 1.7 0 0 0 5.5 5.9v13.4a1.7 1.7 0 0 0 1.7 1.7h9.6a1.7 1.7 0 0 0 1.7-1.7V5.9a1.7 1.7 0 0 0-1.7-1.7H15" />
            <rect x="9" y="2.5" width="6" height="3.4" rx="1.1" />
            <path d="m9.3 12.6 1.9 1.9 3.5-3.9" />
          </g>
          <g id="cloud">
            <path d="M7.2 18.6h9.9a3.9 3.9 0 0 0 .5-7.8 5.8 5.8 0 0 0-11-1.7 3.8 3.8 0 0 0 .6 9.5Z" />
          </g>
        </defs>

        {marks.map((mark, index) => {
          const scale = mark.size / 24;
          return (
            <use
              key={`${mark.id}-${index}`}
              href={`#${mark.id}`}
              opacity={mark.opacity}
              transform={
                `translate(${mark.x} ${mark.y}) ` +
                `rotate(${mark.rotate ?? 0}) ` +
                `scale(${scale}) translate(-12 -12)`
              }
            />
          );
        })}
      </svg>
      <span className="login-sweep" />
    </div>
  );
}
