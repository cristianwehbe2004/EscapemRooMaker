import React from "react";
import "./App.css";
import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";
import EditorPage from "./pages/EditorPage";
import GMPage from "./pages/GMPage";
import LibraryPage from "./pages/LibraryPage";
import PlayerPage from "./pages/PlayerPage";

function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<Navigate to="/player" replace />} />
        <Route path="/player" element={<PlayerPage />} />
        <Route path="/editor" element={<EditorPage />} />
        <Route path="/library" element={<LibraryPage />} />
        <Route path="/gm" element={<GMPage />} />
        <Route path="*" element={<Navigate to="/player" replace />} />
      </Routes>
    </BrowserRouter>
  );
}

export default App;
