import React from "react";
import { render, screen } from "@testing-library/react";
import App from "./App";

jest.mock(
  "react-router-dom",
  () => ({
    BrowserRouter: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
    Routes: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
    Route: ({ element }: { element: React.ReactNode }) => <>{element}</>,
    Navigate: () => null,
  }),
  { virtual: true }
);

jest.mock("./pages/PlayerPage", () => () => <div>Player Experience (Day 4 Slice)</div>);

test("renders player experience header", () => {
  render(<App />);
  expect(screen.getByText(/player experience/i)).toBeInTheDocument();
});
