const state = {
  user: null,
  quizzes: [],
  dashboard: null,
  activeQuizId: null,
  authMode: "login",
  socket: null,
  currentQuestionId: null
};

const $ = (selector) => document.querySelector(selector);
const $$ = (selector) => [...document.querySelectorAll(selector)];

document.addEventListener("DOMContentLoaded", async () => {
  bindEvents();
  setAuthMode("login");
  await loadMe();
});

function bindEvents() {
  $$("[data-auth]").forEach((button) => button.addEventListener("click", () => setAuthMode(button.dataset.auth)));
  $$("[data-view]").forEach((button) => button.addEventListener("click", () => showView(button.dataset.view)));
  $("#authForm").addEventListener("submit", submitAuth);
  $("#logoutBtn").addEventListener("click", logout);
  $("#newQuizBtn").addEventListener("click", () => editQuiz(null));
  $("#addQuestionBtn").addEventListener("click", () => addQuestion());
  $("#saveQuizBtn").addEventListener("click", saveQuiz);
  $("#createSessionBtn").addEventListener("click", createSession);
  $("#joinRoomBtn").addEventListener("click", joinRoom);
  $("#startSessionBtn").addEventListener("click", () => sendRoom("start"));
  $("#nextQuestionBtn").addEventListener("click", () => sendRoom("next"));
  $("#endSessionBtn").addEventListener("click", () => sendRoom("end"));
}

async function api(path, options = {}) {
  const response = await fetch(path, {
    headers: { "Content-Type": "application/json", ...(options.headers || {}) },
    ...options
  });

  if (!response.ok) {
    const error = await response.json().catch(() => ({ message: "Ошибка запроса" }));
    throw new Error(error.message || error.Message || "Ошибка запроса");
  }

  return response.status === 204 ? null : response.json();
}

async function loadMe() {
  state.user = await api("/api/me");
  renderAuthState();

  if (state.user) {
    await loadDashboard();
    showView("dashboard");
  } else {
    $("#authView").classList.add("active");
    $$(".view").forEach((view) => view.classList.remove("active"));
  }
}

function renderAuthState() {
  const logged = Boolean(state.user);
  $("#authView").classList.toggle("active", !logged);
  $(".topbar").classList.toggle("hidden", !logged);
  $("#userBadge").textContent = logged ? `${state.user.displayName} · ${roleLabel(state.user.role)}` : "";
  $$(".organizer-only").forEach((el) => el.classList.toggle("hidden", state.user?.role !== "Organizer"));
}

function setAuthMode(mode) {
  state.authMode = mode;
  $$("[data-auth]").forEach((button) => button.classList.toggle("active", button.dataset.auth === mode));
  $$(".register-field").forEach((field) => field.classList.toggle("hidden", mode !== "register"));
}

async function submitAuth(event) {
  event.preventDefault();
  $("#authError").textContent = "";
  const form = new FormData(event.currentTarget);
  const payload = Object.fromEntries(form.entries());

  try {
    state.user = await api(`/api/${state.authMode}`, { method: "POST", body: JSON.stringify(payload) });
    renderAuthState();
    await loadDashboard();
    showView("dashboard");
  } catch (error) {
    $("#authError").textContent = error.message;
  }
}

async function logout() {
  await api("/api/logout", { method: "POST" });
  location.reload();
}

async function loadDashboard() {
  const [quizzes, history, dashboard] = await Promise.all([
    api("/api/quizzes"),
    api("/api/history"),
    api("/api/dashboard")
  ]);

  state.quizzes = Array.isArray(quizzes) ? quizzes : [];
  state.dashboard = dashboard;
  renderQuizList();
  renderHistory(history);
  renderSessionQuizSelect();
  renderDashboardStats();
}

function renderDashboardStats() {
  const stats = state.dashboard || {};
  const cards = [
    ["Квизы", stats.quizzes ?? state.quizzes.length, "в библиотеке"],
    ["Сессии", stats.sessions ?? 0, "всего"],
    ["Активные", stats.activeSessions ?? 0, "идут сейчас"],
    ["Вопросы", stats.questions ?? 0, "сохранены в базе"]
  ];

  $("#dashboardStats").innerHTML = cards.map(([label, value, caption], index) => `
    <article class="stat-card">
      <span class="badge">${index + 1}</span>
      <strong>${escapeHtml(value)}</strong>
      <span>${escapeHtml(label)} · ${escapeHtml(caption)}</span>
    </article>
  `).join("");
}

function renderQuizList() {
  const list = $("#quizList");
  list.innerHTML = "";

  if (!state.quizzes.length) {
    list.innerHTML = `<article class="list-item"><strong>Пока пусто</strong><span class="meta">Создайте первый квиз, чтобы собрать библиотеку и запускать комнаты.</span></article>`;
    return;
  }

  state.quizzes.forEach((quiz) => {
    const item = document.createElement("article");
    item.className = "list-item";
    const createdAt = quiz.createdAt ? formatDate(quiz.createdAt) : "сегодня";
    item.innerHTML = `
      <div class="item-top">
        <div>
          <strong>${escapeHtml(quiz.title)}</strong>
          <div class="meta">${escapeHtml(quiz.category)} · ${quiz.questions.length} вопросов · ${quiz.secondsPerQuestion} сек.</div>
        </div>
        <span class="status-chip">${escapeHtml(createdAt)}</span>
      </div>
      <div class="actions">
        ${state.user.role === "Organizer" ? `<button data-edit="${quiz.id}">Редактировать</button><button data-run="${quiz.id}">Запустить</button>` : ""}
      </div>`;
    list.append(item);
  });

  $$("[data-edit]").forEach((button) => button.addEventListener("click", () => editQuiz(button.dataset.edit)));
  $$("[data-run]").forEach((button) => button.addEventListener("click", () => {
    $("#sessionQuiz").value = button.dataset.run;
    showView("room");
  }));
}

function renderHistory(items) {
  const list = $("#historyList");
  const values = Array.isArray(items) ? items : [];
  list.innerHTML = values.length ? "" : `<article class="list-item"><strong>История появится здесь</strong><span class="meta">После участия в комнатах и запуска квизов здесь будут видны сессии и результаты.</span></article>`;

  values.forEach((item) => {
    const row = document.createElement("article");
    row.className = "list-item";
    const status = item.status || "Lobby";
    const extra = item.score !== undefined ? `${item.score} баллов` : `${item.players ?? 0} участников`;
    row.innerHTML = `
      <div class="item-top">
        <div>
          <strong>${escapeHtml(item.quiz || "Квиз")}</strong>
          <div class="meta">Комната ${escapeHtml(item.code)} · ${escapeHtml(extra)}</div>
        </div>
        <span class="status-chip">${escapeHtml(status)}</span>
      </div>
      <span class="meta">${item.createdAt ? formatDate(item.createdAt) : ""}</span>`;
    list.append(row);
  });
}

function renderSessionQuizSelect() {
  $("#sessionQuiz").innerHTML = state.quizzes.map((quiz) => `<option value="${quiz.id}">${escapeHtml(quiz.title)}</option>`).join("");
}

function showView(name) {
  $("#authView").classList.remove("active");
  $$(".view").forEach((view) => view.classList.toggle("active", view.id === `${name}View`));
}

function editQuiz(id) {
  const quiz = id ? state.quizzes.find((x) => x.id === id) : null;
  state.activeQuizId = quiz?.id || null;
  $("#quizTitle").value = quiz?.title || "";
  $("#quizCategory").value = quiz?.category || "";
  $("#quizSeconds").value = quiz?.secondsPerQuestion || 30;
  $("#quizRules").value = quiz?.rules || "";
  $("#questionsEditor").innerHTML = "";
  (quiz?.questions || []).forEach(addQuestion);
  if (!quiz) addQuestion();
  showView("builder");
}

function addQuestion(question = null) {
  const node = $("#questionTemplate").content.firstElementChild.cloneNode(true);
  node.dataset.id = question?.id || "";
  node.querySelector(".q-text").value = question?.text || "";
  node.querySelector(".q-kind").value = question?.kind || "Text";
  node.querySelector(".q-image").value = question?.imageUrl || "";
  node.querySelector(".q-mode").value = question?.choiceMode || "Single";
  node.querySelector(".add-option").addEventListener("click", () => addOption(node));
  node.querySelector(".remove-question").addEventListener("click", () => node.remove());
  $("#questionsEditor").append(node);
  (question?.options || [{ text: "", isCorrect: true }, { text: "", isCorrect: false }]).forEach((option) => addOption(node, option));
}

function addOption(questionNode, option = null) {
  const row = document.createElement("div");
  row.className = "option-row";
  row.dataset.id = option?.id || "";
  row.innerHTML = `
    <label>Вариант <input class="opt-text" value="${escapeAttr(option?.text || "")}"></label>
    <label><input class="opt-correct" type="checkbox" ${option?.isCorrect ? "checked" : ""}> Верный</label>
    <button class="ghost" type="button">Удалить</button>`;
  row.querySelector("button").addEventListener("click", () => row.remove());
  questionNode.querySelector(".options").append(row);
}

async function saveQuiz() {
  const questions = $$("#questionsEditor .question-card").map((card) => ({
    id: card.dataset.id,
    text: card.querySelector(".q-text").value,
    kind: card.querySelector(".q-kind").value,
    imageUrl: card.querySelector(".q-image").value,
    choiceMode: card.querySelector(".q-mode").value,
    options: [...card.querySelectorAll(".option-row")].map((row) => ({
      id: row.dataset.id,
      text: row.querySelector(".opt-text").value,
      isCorrect: row.querySelector(".opt-correct").checked
    }))
  }));

  const payload = {
    id: state.activeQuizId,
    title: $("#quizTitle").value,
    category: $("#quizCategory").value,
    secondsPerQuestion: Number($("#quizSeconds").value),
    rules: $("#quizRules").value,
    questions
  };

  await api("/api/quizzes", { method: "POST", body: JSON.stringify(payload) });
  await loadDashboard();
  showView("dashboard");
}

async function createSession() {
  const quizId = $("#sessionQuiz").value;
  if (!quizId) return;
  const session = await api("/api/sessions", { method: "POST", body: JSON.stringify({ quizId }) });
  $("#roomCode").value = session.code;
  await connectSocket(session.code);
}

async function joinRoom() {
  const code = $("#roomCode").value.trim().toUpperCase();
  if (!code) return;
  await api("/api/join", { method: "POST", body: JSON.stringify({ code }) });
  await connectSocket(code);
}

async function connectSocket(code) {
  if (state.socket) state.socket.close();
  const protocol = location.protocol === "https:" ? "wss" : "ws";
  state.socket = new WebSocket(`${protocol}://${location.host}/ws/quiz`);

  state.socket.addEventListener("open", () => {
    sendRoom("join-room", { code });
    $("#roomStatus").textContent = `Подключено к комнате ${code}.`;
    $("#roomCodeLabel").textContent = code;
  });

  state.socket.addEventListener("message", (event) => renderRoom(JSON.parse(event.data)));
  showView("room");
}

function sendRoom(type, extra = {}) {
  const code = extra.code || $("#roomCode").value.trim().toUpperCase();
  if (!state.socket || state.socket.readyState !== WebSocket.OPEN || !code) return;
  state.socket.send(JSON.stringify({ type, code, ...extra }));
}

function renderRoom(message) {
  if (message.type !== "state") return;
  const { session, quiz, question, leaderboard, answeredCount } = message;
  const code = session?.code || $("#roomCode").value;
  if (code) {
    $("#roomCode").value = code;
    $("#roomCodeLabel").textContent = code;
  }

  $("#sessionStateBadge").textContent = session?.status || "Lobby";
  $("#sessionProgressBadge").textContent = quiz ? `${Math.max(session.currentQuestionIndex + 1, 0)} / ${quiz.totalQuestions}` : "0 / 0";
  $("#answeredBadge").textContent = String(answeredCount ?? 0);
  $("#roomStatus").innerHTML = quiz
    ? `${escapeHtml(quiz.title)} · <span class="badge">${escapeHtml(session.status)}</span> · вопрос ${Math.max(session.currentQuestionIndex + 1, 0)} из ${quiz.totalQuestions}`
    : "Ожидание квиза.";

  renderLeaderboard(leaderboard || []);
  renderQuestion(question, session?.status, quiz);
}

function renderLeaderboard(players) {
  $("#leaderboard").innerHTML = players.length
    ? players.map((p, index) => `<div class="leader"><span>${index + 1}. ${escapeHtml(p.displayName)}</span><strong>${p.score}</strong></div>`).join("")
    : `<p class="hint">Участники появятся после подключения к комнате.</p>`;
}

function renderQuestion(question, status, quiz) {
  const panel = $("#questionPanel");

  if (!question || status === "Lobby") {
    panel.innerHTML = `
      <div class="question-shell">
        <div class="question-banner">
          <div>
            <h2>Ожидание старта</h2>
            <p class="hint">Организатор запускает квиз, а участники видят текущий вопрос синхронно.</p>
          </div>
          <span class="pill">${escapeHtml(quiz?.title || "Комната готова")}</span>
        </div>
      </div>`;
    return;
  }

  if (status === "Finished") {
    panel.innerHTML = `
      <div class="question-shell">
        <div class="question-banner">
          <div>
            <h2>Квиз завершён</h2>
            <p class="hint">Результаты сохранены в SQLite и уже доступны в истории.</p>
          </div>
          <span class="pill">Готово</span>
        </div>
      </div>`;
    loadDashboard();
    return;
  }

  state.currentQuestionId = question.id;
  const inputType = question.choiceMode === "Multiple" ? "checkbox" : "radio";
  panel.innerHTML = `
    <div class="question-shell">
      <div class="question-banner">
        <div>
          <h2>${escapeHtml(question.text)}</h2>
          <p class="hint">${question.choiceMode === "Multiple" ? "Можно выбрать несколько вариантов." : "Нужен один ответ."}</p>
        </div>
        <span class="pill">${question.kind === "Image" ? "Изображение" : "Текст"}</span>
      </div>
      ${question.kind === "Image" && question.imageUrl ? `<img class="question-image" src="${escapeAttr(question.imageUrl)}" alt="Изображение вопроса">` : ""}
      <div class="answer-list">
        ${question.options.map((option) => `
          <label><input type="${inputType}" name="answer" value="${option.id}">${escapeHtml(option.text)}</label>
        `).join("")}
      </div>
      <div class="actions">
        <button id="submitAnswerBtn">Отправить ответ</button>
      </div>
    </div>`;
  $("#submitAnswerBtn").addEventListener("click", submitAnswer);
}

function submitAnswer() {
  const optionIds = $$("#questionPanel input[name='answer']:checked").map((input) => input.value);
  sendRoom("answer", { questionId: state.currentQuestionId, optionIds });
  const button = $("#submitAnswerBtn");
  if (button) {
    button.disabled = true;
    button.textContent = "Ответ принят";
  }
}

function roleLabel(role) {
  return role === "Organizer" ? "организатор" : "участник";
}

function formatDate(value) {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "";
  return new Intl.DateTimeFormat("ru-RU", { day: "2-digit", month: "short", hour: "2-digit", minute: "2-digit" }).format(date);
}

function escapeHtml(value) {
  return String(value ?? "").replace(/[&<>"']/g, (char) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#039;" }[char]));
}

function escapeAttr(value) {
  return escapeHtml(value).replace(/`/g, "&#096;");
}
