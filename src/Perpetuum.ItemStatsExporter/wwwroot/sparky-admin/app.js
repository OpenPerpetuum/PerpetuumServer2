(() => {
  'use strict';

  const tokenKey = 'sparky-admin-token';
  const state = {
    token: sessionStorage.getItem(tokenKey),
    storageAvailable: false,
    selectedTrace: null,
    traces: [],
    profiles: [],
    profileDetails: null,
    bundles: [],
    evaluations: [],
    selectedEvaluationIds: new Set()
  };
  const $ = (id) => document.getElementById(id);
  const dialog = $('token-dialog');

  function setMessage(message, error = false, target = 'message') {
    const element = $(target);
    element.textContent = message;
    element.classList.toggle('error', error);
  }

  async function api(path, { method = 'GET', body, contentType = 'application/json' } = {}) {
    const headers = { Authorization: `Bearer ${state.token}`, Accept: 'application/json' };
    if (body !== undefined && contentType) headers['Content-Type'] = contentType;
    const response = await fetch(path, {
      method,
      headers,
      body,
      cache: 'no-store'
    });
    if (response.status === 401) {
      logout('The token was rejected. Enter the admin token again.');
      throw new Error('unauthorized');
    }
    const payload = response.status === 204 ? null : await response.json().catch(() => null);
    if (!response.ok) {
      const code = payload?.error || `Request failed (${response.status}).`;
      throw new Error(code);
    }
    return payload;
  }

  function logout(message = '') {
    sessionStorage.removeItem(tokenKey);
    state.token = null;
    state.storageAvailable = false;
    state.selectedTrace = null;
    state.traces = [];
    state.profiles = [];
    state.profileDetails = null;
    state.bundles = [];
    state.evaluations = [];
    state.selectedEvaluationIds.clear();
    $('connection-state').textContent = 'Disconnected';
    $('connection-state').classList.remove('connected');
    $('logout').hidden = true;
    $('export-trace').disabled = true;
    $('trace-list').replaceChildren(Object.assign(document.createElement('p'), { className: 'muted', textContent: 'Connect to Sparky to inspect request history.' }));
    $('trace-detail').replaceChildren(Object.assign(document.createElement('p'), { className: 'muted', textContent: 'Select a trace to inspect its timeline.' }));
    $('capture-indicator').hidden = true;
    $('review-tools').hidden = true;
    $('evaluation-turn-list').replaceChildren();
    $('evaluation-note').value = '';
    $('tunnel-list').replaceChildren(Object.assign(document.createElement('p'), { className: 'muted', textContent: 'Connect to Sparky to see tunnel status.' }));
    $('evaluation-list').replaceChildren(Object.assign(document.createElement('p'), { className: 'muted', textContent: 'Load reviewed cases for the selected profile.' }));
    $('replay-results').replaceChildren();
    $('bundle-list').replaceChildren();
    $('revision-select').replaceChildren();
    $('replay-draft-revision').replaceChildren();
    $('profile-bundle').replaceChildren();
    $('profile-route-aliases').value = '[]';
    $('profile-persona').value = '';
    $('profile-model-id').value = '';
    $('profile-history-turns').value = '';
    $('profile-history-characters').value = '';
    $('profile-answer-temperature').value = '';
    $('profile-answer-top-p').value = '';
    $('profile-summary').textContent = 'Connect to Sparky to inspect Dev and Live profiles.';
    $('profile-operation-result').textContent = '';
    $('review-result').textContent = '';
    $('feedback-note').value = '';
    $('capture-indicator').hidden = true;
    for (const id of ['service-status', 'model-status', 'bundle-status', 'storage-status', 'dev-capture', 'live-capture']) $(id).textContent = '—';
    if (message) setMessage(message, true);
    if (!dialog.open) dialog.showModal();
  }

  async function refreshStatus() {
    try {
      const [health, tunnels] = await Promise.all([api('/admin/api/health'), api('/admin/api/tunnels')]);
      $('service-status').textContent = health.status;
      $('model-status').textContent = health.modelId;
      $('bundle-status').textContent = health.bundleVersion;
      $('storage-status').textContent = health.traceStoreAvailable ? 'Available' : 'Unavailable';
      state.storageAvailable = health.traceStoreAvailable;
      $('dev-capture').textContent = health.traceContentCapture.dev ? 'Enabled' : 'Metadata only';
      $('live-capture').textContent = health.traceContentCapture.live ? 'Enabled' : 'Metadata only';
      $('tunnel-list').replaceChildren(...tunnels.map(tunnel => {
        const item = document.createElement('div');
        item.className = 'tunnel-item';
        const name = document.createElement('strong');
        name.textContent = tunnel.name;
        const status = document.createElement('span');
        status.className = `state ${tunnel.state}`;
        status.textContent = tunnel.enabled ? tunnel.state : 'disabled';
        item.append(name, status);
        return item;
      }));
      $('connection-state').textContent = 'Connected';
      $('connection-state').classList.add('connected');
      $('logout').hidden = false;
      setMessage('Status refreshed.');
      await loadProfiles();
    } catch (error) {
      if (error.message !== 'unauthorized') setMessage(error.message, true);
    }
  }

  function makeQuery() {
    const query = new URLSearchParams();
    query.set('environment', $('environment').value);
    query.set('limit', $('limit').value);
    for (const [key, id] of [['fromUtc', 'from-utc'], ['toUtc', 'to-utc']]) {
      const value = $(id).value;
      if (value) query.set(key, new Date(value).toISOString());
    }
    for (const [key, id] of [['source', 'source'], ['status', 'status-filter'], ['label', 'label-filter']]) {
      const value = $(id).value.trim();
      if (value) query.set(key, value);
    }
    return query;
  }

  function renderTraces(traces) {
    state.traces = traces;
    const list = $('trace-list');
    if (!traces.length) {
      list.replaceChildren(Object.assign(document.createElement('p'), { className: 'muted', textContent: 'No traces matched these filters.' }));
      return;
    }
    list.replaceChildren(...traces.map(trace => {
      const button = document.createElement('button');
      button.type = 'button';
      button.className = 'trace-row';
      button.dataset.requestId = trace.requestId;
      const title = document.createElement('strong');
      title.textContent = trace.question || `${trace.source || 'Request'} · ${trace.outcome || 'pending'}`;
      const metadata = document.createElement('small');
      metadata.textContent = `${trace.environment} · ${trace.source || 'unknown'} · ${trace.outcome || 'unknown'} · ${new Date(trace.firstRecordedAtUtc).toLocaleString()}`;
      button.append(title, metadata);
      if (trace.labels?.length) {
        const label = document.createElement('small');
        label.textContent = `Feedback: ${trace.labels.join(', ')}`;
        button.append(label);
      }
      button.addEventListener('click', () => loadTrace(trace.requestId, trace.environment, button));
      return button;
    }));
  }

  async function loadTraces() {
    try {
      const traces = await api(`/admin/api/traces?${makeQuery()}`);
      renderTraces(traces);
      setMessage(`Loaded ${traces.length} trace${traces.length === 1 ? '' : 's'}.`);
    } catch (error) {
      if (error.message !== 'unauthorized') setMessage(error.message, true);
    }
  }

  function appendText(parent, label, value) {
    if (value == null || value === '') return;
    const block = document.createElement('div');
    block.className = 'event-body';
    const heading = document.createElement('strong');
    heading.textContent = `${label}: `;
    const content = document.createElement('span');
    content.textContent = value;
    block.append(heading, content);
    parent.append(block);
  }

  async function loadTrace(requestId, environment, button) {
    try {
      const result = await api(`/admin/api/traces/${encodeURIComponent(requestId)}?environment=${encodeURIComponent(environment)}`);
      state.selectedTrace = { requestId, environment, ...result };
      const capturedQuestion = result.trace.events.find(event => event.question)?.question ?? '';
      resetEvaluationTurns(capturedQuestion);
      $('export-trace').disabled = false;
      $('review-tools').hidden = false;
      document.querySelectorAll('.trace-row').forEach(row => row.classList.toggle('selected', row === button));
      const detail = $('trace-detail');
      detail.replaceChildren();
      const header = document.createElement('h3');
      header.textContent = `Request ${result.trace.requestId}`;
      detail.append(header);
      if (!result.trace.events.some(event => event.question || event.answer)) {
        $('capture-indicator').hidden = false;
        $('capture-indicator').textContent = 'Question and answer content were not captured for this trace. Metadata is available.';
      } else {
        $('capture-indicator').hidden = true;
      }
      for (const event of result.trace.events) {
        const card = document.createElement('article');
        card.className = 'timeline-event';
        const headingRow = document.createElement('div');
        headingRow.className = 'event-heading';
        const stage = document.createElement('span');
        stage.textContent = `${event.source} · ${event.stage} · ${event.outcome}`;
        const time = document.createElement('time');
        time.textContent = new Date(event.recordedAtUtc).toLocaleTimeString();
        headingRow.append(stage, time);
        card.append(headingRow);
        appendText(card, 'Question', event.question);
        appendText(card, 'Answer', event.answer);
        appendText(card, 'Query', event.searchQuery);
        appendText(card, 'Parsed route', [event.intent, event.focus].filter(Boolean).join(' · '));
        appendText(card, 'Effective route', [event.effectiveIntent, event.effectiveFocus].filter(Boolean).join(' · '));
        appendText(card, 'Resolver', [event.resolverCategory, event.resolverOutcome].filter(Boolean).join(' · '));
        appendText(card, 'Context used', event.contextMessageCount == null && event.contextCharacterCount == null
          ? null
          : `${event.contextMessageCount ?? 0}/${event.conversationHistoryTurns ?? '?'} messages · ${event.contextCharacterCount ?? 0}/${event.conversationHistoryCharacters ?? '?'} characters`);
        appendText(card, 'Context flags', [
          event.hasContextSummary ? 'summary' : null,
          event.hasPendingClarification ? 'pending clarification' : null,
          event.contextBudgetTrimmed ? 'trimmed to budget' : null
        ].filter(Boolean).join(' · '));
        appendText(card, 'Comparison route', [event.comparisonSubjectCount == null ? null : `${event.comparisonSubjectCount} subjects`, event.comparisonAttributeCategory, event.comparisonResultCategory].filter(Boolean).join(' · '));
        appendText(card, 'Selected sources', event.selectedSourceIds?.join(', '));
        if (event.contextTurns?.length) {
          appendText(card, 'Context turns', event.contextTurns.map(turn => `${turn.role}: ${turn.content}`).join('\n'));
        }
        appendText(card, 'Context summary', event.contextSummary);
        if (event.pendingClarification) {
          appendText(card, 'Pending clarification', [
            event.pendingClarification.originalQuestion,
            event.pendingClarification.candidateNames?.join(', ')
          ].filter(Boolean).join(' · '));
        }
        if (event.comparisonDiagnostics) {
          const diagnostics = event.comparisonDiagnostics;
          appendText(card, 'Comparison subjects', diagnostics.subjects?.map(subject => `${subject.displayName} (${subject.referenceSource})`).join(' vs '));
          appendText(card, 'Comparison metric', diagnostics.metricPhrase);
          appendText(card, 'Comparison outcome', diagnostics.resultCategory);
          if (diagnostics.facts?.length) {
            appendText(card, 'Verified comparison facts', diagnostics.facts.map(fact => {
              const values = (fact.values ?? []).join(' vs ');
              const unit = fact.unit ? ` ${fact.unit}` : '';
              const difference = fact.difference == null ? '' : ` · difference ${fact.difference}${unit}`;
              const leader = fact.leadingSubjectName ? ` · directional leader: ${fact.leadingSubjectName}` : '';
              return `${fact.attributeLabel}: ${values}${unit}${difference}${leader}`;
            }).join('\n'));
          }
        }
        appendText(card, 'Model', event.modelId);
        appendText(card, 'Profile revision', event.configRevision);
        appendText(card, 'Knowledge bundle', event.bundleVersion);
        if (event.answerTemperature != null || event.answerTopP != null) {
          appendText(card, 'Answer tuning', `temperature ${event.answerTemperature} · top-p ${event.answerTopP} · history ${event.conversationHistoryTurns}/${event.conversationHistoryCharacters}`);
        }
        appendText(card, 'Revision', event.configRevision);
        appendText(card, 'Bundle', event.bundleVersion);
        appendText(card, 'Elapsed', event.elapsedMilliseconds == null ? null : `${event.elapsedMilliseconds} ms`);
        detail.append(card);
      }
      $('export-trace').disabled = false;
    } catch (error) {
      if (error.message !== 'unauthorized') setMessage(error.message, true);
    }
  }

  async function exportTrace() {
    if (!state.selectedTrace || !state.token) return;
    const { requestId, environment } = state.selectedTrace;
    const includeContent = $('export-content').checked;
    const url = `/admin/api/traces/${encodeURIComponent(requestId)}/export?environment=${encodeURIComponent(environment)}&includeContent=${includeContent}`;
    try {
      const response = await fetch(url, { headers: { Authorization: `Bearer ${state.token}`, Accept: 'application/json' }, cache: 'no-store' });
      if (response.status === 401) {
        logout('The token was rejected. Enter the admin token again.');
        return;
      }
      if (!response.ok) throw new Error(`Export failed (${response.status}).`);
      const blob = await response.blob();
      const objectUrl = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = objectUrl;
      link.download = `sparky-trace-${requestId}.json`;
      link.click();
      URL.revokeObjectURL(objectUrl);
      setMessage('Sanitized trace export downloaded.');
    } catch (error) {
      setMessage(error.message, true);
    }
  }

  function selectedProfile() { return $('profile-select').value; }

  function profileRequest() {
    return {
      schemaVersion: 2,
      modelId: $('profile-model-id').value.trim(),
      maxAnswerCharacters: Number($('profile-answer-limit').value),
      maxCompletionTokens: Number($('profile-token-limit').value),
      inferenceTimeoutSeconds: Number($('profile-timeout').value),
      maxConcurrentRequests: Number($('profile-concurrency').value),
      conversationHistoryTurns: Number($('profile-history-turns').value),
      conversationHistoryCharacters: Number($('profile-history-characters').value),
      answerTemperature: Number($('profile-answer-temperature').value),
      answerTopP: Number($('profile-answer-top-p').value),
      captureContent: $('profile-capture').value === 'true',
      contentRetentionDays: Number($('profile-content-retention').value),
      metadataRetentionDays: Number($('profile-metadata-retention').value),
      personaStyle: $('profile-persona').value,
      retrieval: {
        maxPassages: Number($('profile-max-passages').value),
        maxCharacters: Number($('profile-max-retrieval-chars').value),
        minimumScore: Number($('profile-min-score').value),
        sourceCategories: Array.from($('profile-source-categories').selectedOptions, option => option.value)
      },
      routeAliases: JSON.parse($('profile-route-aliases').value),
      bundleVersion: $('profile-bundle').value
    };
  }

  function fillProfile(configuration) {
    if (!configuration) return;
    $('profile-model-id').value = configuration.modelId ?? '';
    $('profile-answer-limit').value = configuration.maxAnswerCharacters ?? '';
    $('profile-token-limit').value = configuration.maxCompletionTokens ?? '';
    $('profile-timeout').value = configuration.inferenceTimeoutSeconds ?? '';
    $('profile-concurrency').value = configuration.maxConcurrentRequests ?? '';
    $('profile-history-turns').value = configuration.conversationHistoryTurns ?? 8;
    $('profile-history-characters').value = configuration.conversationHistoryCharacters ?? 4000;
    $('profile-answer-temperature').value = configuration.answerTemperature ?? 0.2;
    $('profile-answer-top-p').value = configuration.answerTopP ?? 0.9;
    $('profile-capture').value = String(configuration.captureContent ?? false);
    $('profile-content-retention').value = configuration.contentRetentionDays ?? '';
    $('profile-metadata-retention').value = configuration.metadataRetentionDays ?? '';
    $('profile-max-passages').value = configuration.retrieval?.maxPassages ?? '';
    $('profile-max-retrieval-chars').value = configuration.retrieval?.maxCharacters ?? '';
    $('profile-min-score').value = configuration.retrieval?.minimumScore ?? '';
    $('profile-persona').value = configuration.personaStyle ?? '';
    $('profile-route-aliases').value = JSON.stringify(configuration.routeAliases ?? [], null, 2);
    const categories = new Set(configuration.retrieval?.sourceCategories ?? []);
    for (const option of $('profile-source-categories').options) option.selected = categories.has(option.value);
    const bundle = $('profile-bundle');
    const version = configuration.bundleVersion ?? '';
    if (version && !Array.from(bundle.options).some(option => option.value === version)) {
      bundle.add(new Option(`${version} (active)`, version));
    }
    bundle.value = version;
  }

  function renderProfile(details) {
    state.profileDetails = details;
    const summary = details.summary;
    const configuration = details.draftConfiguration ?? details.activeConfiguration;
    $('profile-summary').textContent = `${summary.profile}: ${summary.enabled ? 'enabled' : 'disabled'} · active ${summary.activeRevisionId ?? 'none'} · bundle ${summary.bundleVersion ?? 'none'}${details.draftRevisionId ? ` · draft ${details.draftRevisionId}` : ''}`;
    fillProfile(configuration);
    const describeTuning = value => value
      ? `${value.conversationHistoryTurns ?? 8} history messages / ${value.conversationHistoryCharacters ?? 4000} chars · temperature ${value.answerTemperature ?? 0.2} · top-p ${value.answerTopP ?? 0.9}`
      : 'not configured';
    $('profile-effective-settings').textContent = `Active settings: ${describeTuning(details.activeConfiguration)} · Draft settings: ${details.draftConfiguration ? describeTuning(details.draftConfiguration) : 'none'}`;
    const revisions = $('revision-select');
    const revisionOptions = details.revisions.map(revision => {
      const option = new Option(`${revision.isActive ? 'ACTIVE' : revision.isDraft ? 'DRAFT' : 'saved'} · ${revision.revisionId} · ${new Date(revision.createdAtUtc).toLocaleString()}`, revision.revisionId);
      option.dataset.draft = String(revision.isDraft);
      return option;
    });
    revisions.replaceChildren(...revisionOptions);
    if (details.draftRevisionId) revisions.value = details.draftRevisionId;
    else if (summary.activeRevisionId) revisions.value = summary.activeRevisionId;
    const replayRevisions = details.revisions.filter(revision => revision.isDraft)
      .map(revision => new Option(`${revision.revisionId} · ${new Date(revision.createdAtUtc).toLocaleString()}`, revision.revisionId));
    $('replay-draft-revision').replaceChildren(...replayRevisions);
    if (details.draftRevisionId) $('replay-draft-revision').value = details.draftRevisionId;

    const disabled = !summary.enabled || !state.storageAvailable;
    if (!state.storageAvailable)
      $('profile-summary').textContent += ' · edits unavailable until trace storage recovers';
    const liveProfile = summary.profile === 'Live';
    if (liveProfile)
      $('profile-summary').textContent += ' · read-only; change Live through explicit Dev promotion or rollback';
    for (const element of document.querySelectorAll('#profiles-panel input, #profiles-panel select, #profiles-panel textarea, #profiles-panel button')) {
      if (element.id === 'profile-select' || element.id === 'refresh-profiles') continue;
      const sharedBundleStaging = element.id === 'bundle-file' || element.id === 'stage-bundle';
      const rollbackReason = element.id === 'profile-reason';
      element.disabled = disabled || (liveProfile && !sharedBundleStaging && !rollbackReason);
    }
    $('save-draft').disabled = disabled || liveProfile;
    $('validate-draft').disabled = disabled || liveProfile;
    $('activate-draft').disabled = disabled || liveProfile || !revisionIsDraft();
    $('promote-live').disabled = disabled || summary.profile !== 'Dev' || !revisionIsAvailable();
    $('rollback-profile').disabled = disabled || details.revisions.filter(revision => revision.activatedAtUtc).length < 2;
    $('profile-reason').disabled = disabled;
    updateReplayButton();
  }

  function revisionIsDraft() {
    const option = $('revision-select').selectedOptions[0];
    return option?.dataset.draft === 'true';
  }

  function revisionIsAvailable() { return Boolean($('revision-select').value); }

  async function loadBundles() {
    state.bundles = await api('/admin/api/bundles');
    const select = $('profile-bundle');
    const previous = select.value;
    select.replaceChildren(...state.bundles.map(bundle => new Option(`${bundle.version} · ${bundle.sizeBytes.toLocaleString()} bytes`, bundle.version)));
    if (previous && Array.from(select.options).some(option => option.value === previous)) select.value = previous;
    $('bundle-list').replaceChildren(...(state.bundles.length ? state.bundles.map(bundle => {
      const item = document.createElement('span');
      item.className = 'bundle-item';
      item.textContent = `${bundle.version} · ${new Date(bundle.stagedAtUtc).toLocaleString()} · ${bundle.sizeBytes.toLocaleString()} bytes`;
      return item;
    }) : [Object.assign(document.createElement('p'), { className: 'muted', textContent: 'No staged bundles loaded.' })]));
  }

  async function loadProfile() {
    const profile = selectedProfile();
    const [details] = await Promise.all([api(`/admin/api/profiles/${encodeURIComponent(profile)}`), loadBundles()]);
    renderProfile(details);
    await loadEvaluations();
  }

  async function loadProfiles() {
    state.profiles = await api('/admin/api/profiles');
    const live = state.profiles.find(profile => profile.profile === 'Live');
    $('profile-select').options[1].textContent = live?.enabled ? 'Live' : 'Live (disabled)';
    await loadProfile();
  }

  function getReason() { return $('profile-reason').value.trim() || null; }

  async function saveDraft() {
    try {
      const configuration = profileRequest();
      const result = await api(`/admin/api/profiles/${encodeURIComponent(selectedProfile())}/draft`, {
        method: 'PUT', body: JSON.stringify({ configuration, reason: getReason() })
      });
      setMessage(`Draft saved: ${result.revisionId}`, false, 'profile-operation-result');
      await loadProfile();
    } catch (error) {
      if (error.message !== 'unauthorized') setMessage(error.message, true, 'profile-operation-result');
    }
  }

  async function validateDraft() {
    try {
      const result = await api(`/admin/api/profiles/${encodeURIComponent(selectedProfile())}/validate`, {
        method: 'POST', body: JSON.stringify(profileRequest())
      });
      setMessage(result.valid ? 'Configuration is valid.' : result.errors.join(' '), !result.valid, 'profile-operation-result');
    } catch (error) {
      if (error.message !== 'unauthorized') setMessage(error.message, true, 'profile-operation-result');
    }
  }

  async function activateRevision() {
    const revisionId = $('revision-select').value;
    if (selectedProfile() === 'Live')
      return setMessage('Live revisions change only through explicit Dev promotion. Use rollback to restore a prior revision.', true, 'profile-operation-result');
    if (!revisionIsDraft() || !revisionId) return setMessage('Choose a saved draft revision first.', true, 'profile-operation-result');
    try {
      const result = await api(`/admin/api/profiles/${encodeURIComponent(selectedProfile())}/activate`, {
        method: 'POST', body: JSON.stringify({ revisionId, reason: getReason() })
      });
      setMessage(`Activated revision ${result.revisionId}.`, false, 'profile-operation-result');
      await loadProfile();
    } catch (error) {
      if (error.message !== 'unauthorized') setMessage(error.message, true, 'profile-operation-result');
    }
  }

  async function promoteRevision() {
    const sourceRevisionId = $('revision-select').value;
    if (selectedProfile() !== 'Dev' || !sourceRevisionId) return;
    if (!window.confirm(`Promote Dev revision ${sourceRevisionId} to Live? Live answers will change.`)) return;
    try {
      const result = await api('/admin/api/profiles/live/promote', {
        method: 'POST', body: JSON.stringify({ sourceRevisionId, reason: getReason() })
      });
      setMessage(`Promoted as Live revision ${result.revisionId}.`, false, 'profile-operation-result');
      $('profile-select').value = 'Live';
      await loadProfile();
    } catch (error) {
      if (error.message !== 'unauthorized') setMessage(error.message, true, 'profile-operation-result');
    }
  }

  async function rollbackProfile() {
    if (selectedProfile() === 'Live' && !window.confirm('Roll back Live to its previous activated revision?')) return;
    try {
      const result = await api(`/admin/api/profiles/${encodeURIComponent(selectedProfile())}/rollback`, {
        method: 'POST', body: JSON.stringify({ reason: getReason() })
      });
      setMessage(`Rolled back as revision ${result.revisionId}.`, false, 'profile-operation-result');
      await loadProfile();
    } catch (error) {
      if (error.message !== 'unauthorized') setMessage(error.message, true, 'profile-operation-result');
    }
  }

  async function stageBundle() {
    const file = $('bundle-file').files[0];
    if (!file) return setMessage('Choose a knowledge bundle JSON file.', true, 'profile-operation-result');
    try {
      const bytes = await file.arrayBuffer();
      const bundle = await api('/admin/api/bundles', { method: 'POST', body: bytes, contentType: 'application/json' });
      setMessage(`Bundle staged and validated: ${bundle.version}`, false, 'profile-operation-result');
      $('bundle-file').value = '';
      await loadBundles();
      $('profile-bundle').value = bundle.version;
    } catch (error) {
      if (error.message !== 'unauthorized') setMessage(error.message, true, 'profile-operation-result');
    }
  }

  async function loadEvaluations() {
    const profile = selectedProfile();
    state.evaluations = await api(`/admin/api/evaluations?environment=${encodeURIComponent(profile)}&limit=100`);
    const available = new Set(state.evaluations.map(example => example.exampleId));
    state.selectedEvaluationIds = new Set(Array.from(state.selectedEvaluationIds).filter(id => available.has(id)));
    renderEvaluations();
  }

  function renderEvaluations() {
    const container = $('evaluation-list');
    if (!state.evaluations.length) {
      container.replaceChildren(Object.assign(document.createElement('p'), { className: 'muted', textContent: 'No reviewed evaluation cases for this profile.' }));
      updateReplayButton();
      return;
    }
    container.replaceChildren(...state.evaluations.map(example => {
      const row = document.createElement('label');
      row.className = 'evaluation-row';
      const check = document.createElement('input');
      check.type = 'checkbox';
      check.checked = state.selectedEvaluationIds.has(example.exampleId);
      check.addEventListener('change', () => {
        if (check.checked) state.selectedEvaluationIds.add(example.exampleId);
        else state.selectedEvaluationIds.delete(example.exampleId);
        updateReplayButton();
      });
      const copy = document.createElement('span');
      const question = document.createElement('strong');
      question.textContent = example.question;
      const meta = document.createElement('small');
      meta.textContent = `${example.turns?.length ?? 1} turn${(example.turns?.length ?? 1) === 1 ? '' : 's'} · ${example.expectedIntent || 'intent not set'} · ${example.expectedProperties.join(', ') || 'no expected properties'} · ${new Date(example.createdAtUtc).toLocaleString()}`;
      copy.append(question, meta);
      row.append(check, copy);
      return row;
    }));
    updateReplayButton();
  }

  function updateReplayButton() {
    const profile = state.profileDetails?.summary;
    $('run-replay').disabled = !state.storageAvailable || !profile?.enabled || !$('replay-draft-revision').value || state.selectedEvaluationIds.size === 0;
  }

  async function runReplay() {
    const details = state.profileDetails;
    const draftRevisionId = $('replay-draft-revision').value;
    if (!details || !draftRevisionId || state.selectedEvaluationIds.size === 0) return;
    const selected = Array.from(state.selectedEvaluationIds).slice(0, 100);
    $('run-replay').disabled = true;
    setMessage(`Replaying ${selected.length} case${selected.length === 1 ? '' : 's'} against the active and draft revisions.`, false, 'profile-operation-result');
    try {
      const replay = await api('/admin/api/replays', {
        method: 'POST', body: JSON.stringify({ environment: selectedProfile(), draftRevisionId, exampleIds: selected })
      });
      const persisted = await api(`/admin/api/replays/${encodeURIComponent(replay.replayId)}?environment=${encodeURIComponent(replay.environment)}`);
      renderReplay(persisted);
      setMessage(`Replay ${persisted.replayId} saved.`, false, 'profile-operation-result');
    } catch (error) {
      if (error.message !== 'unauthorized') setMessage(error.message, true, 'profile-operation-result');
    } finally {
      updateReplayButton();
    }
  }

  function renderReplay(replay) {
    const container = $('replay-results');
    container.replaceChildren();
    const heading = document.createElement('h3');
    heading.textContent = `Replay ${replay.replayId} · active ${replay.activeRevisionId} → candidate ${replay.candidateRevisionId}`;
    container.append(heading);
    if (replay.contentExpired) {
      container.append(Object.assign(document.createElement('p'), { className: 'muted', textContent: 'Replay content expired under the configured retention policy.' }));
      return;
    }
    for (const result of replay.results) {
      const card = document.createElement('article');
      card.className = 'replay-case';
      const question = document.createElement('strong');
      question.textContent = `${result.turns?.length ?? 1} turn${(result.turns?.length ?? 1) === 1 ? '' : 's'} · ${result.question}`;
      const expected = document.createElement('p');
      expected.className = 'muted';
      expected.textContent = `Reviewed expectation: ${result.expectedIntent || 'not specified'} · ${result.expectedProperties.join(', ') || 'no properties'}`;
      card.append(question, expected);
      const turns = result.turns?.length ? result.turns : [{
        question: result.question,
        expected: { expectedScope: null, expectedIntent: result.expectedIntent, expectedProperties: result.expectedProperties },
        active: result.active,
        candidate: result.candidate
      }];
      turns.forEach((turn, index) => {
        const turnCard = document.createElement('section');
        turnCard.className = 'replay-turn';
        const turnHeading = document.createElement('h4');
        turnHeading.textContent = `Turn ${index + 1}/${turns.length}: ${turn.question}`;
        const expectation = turn.expected || {};
        const expectationText = document.createElement('p');
        expectationText.className = 'muted';
        expectationText.textContent = `Expected: ${expectation.expectedScope || 'scope any'} · ${expectation.expectedIntent || 'intent any'} · ${expectation.expectedEntity || 'entity any'} · sources ${expectation.expectedSourceIds?.join(', ') || 'any'} · required ${expectation.requiredFacts?.join(', ') || 'none'} · forbidden ${expectation.forbiddenClaims?.join(', ') || 'none'} · properties ${expectation.expectedProperties?.join(', ') || 'none'}`;
        const routeChanged = turn.active.intent !== turn.candidate.intent || turn.active.scope !== turn.candidate.scope || turn.active.status !== turn.candidate.status;
        const activeSources = [...(turn.active.selectedSourceIds ?? [])].sort().join('|');
        const candidateSources = [...(turn.candidate.selectedSourceIds ?? [])].sort().join('|');
        const changeSummary = document.createElement('p');
        changeSummary.className = 'muted';
        changeSummary.textContent = `Active vs Draft: route ${routeChanged ? 'changed' : 'same'} · evidence ${activeSources === candidateSources ? 'same' : 'changed'} · resolved entity ${turn.active.structuredFactsSummary === turn.candidate.structuredFactsSummary ? 'same' : 'changed'} · wording ${turn.active.answer === turn.candidate.answer ? 'same' : 'changed'}`;
        turnCard.append(turnHeading, expectationText, changeSummary);
        const columns = document.createElement('div');
        columns.className = 'replay-columns';
        for (const [label, output] of [['Active', turn.active], ['Draft', turn.candidate]]) {
          const column = document.createElement('section');
          column.className = 'replay-column';
          const title = document.createElement('h4');
          title.textContent = `${label} · ${output.status} · ${output.scope || 'scope unknown'} / ${output.intent || 'intent unknown'}`;
          const entities = document.createElement('p');
          entities.textContent = `Entities: ${output.entities?.join(', ') || 'none'} · resolved: ${output.structuredFactsSummary || 'none'}`;
          const query = document.createElement('p');
          query.textContent = `Query: ${output.searchQuery || 'none'}`;
          const sources = document.createElement('p');
          sources.textContent = `Sources: ${output.selectedSourceIds?.join(', ') || 'none'}`;
          const metadata = output.metadata;
          const tuning = document.createElement('p');
          tuning.className = 'muted';
          tuning.textContent = metadata ? `Model ${metadata.modelId} · revision ${metadata.profileRevisionId} · bundle ${metadata.bundleVersion} · temp ${metadata.answerTemperature} · top-p ${metadata.answerTopP} · history ${metadata.conversationHistoryTurns}/${metadata.conversationHistoryCharacters}` : '';
          const answer = document.createElement('p');
          answer.textContent = output.answer || 'No answer text.';
          column.append(title, entities, query, sources, tuning, answer);
          columns.append(column);
        }
        turnCard.append(columns);
        card.append(turnCard);
      });
      container.append(card);
    }
  }

  const intents = ['', 'StructuredItem', 'StructuredRobot', 'StructuredModuleCatalog', 'MechanicsLore', 'Combined', 'SmallTalk', 'Unsupported'];
  const scopes = ['', 'Game', 'SmallTalk', 'OutOfScope', 'Ambiguous'];
  function makeField(labelText, field, value = '', multiline = false, placeholder = '') {
    const label = document.createElement('label');
    label.textContent = labelText;
    const input = document.createElement(multiline ? 'textarea' : 'input');
    input.dataset.field = field;
    input.value = value ?? '';
    if (multiline) input.rows = 2;
    if (placeholder) input.placeholder = placeholder;
    label.append(input);
    return label;
  }

  function makeSelect(labelText, field, values, selected = '') {
    const label = document.createElement('label');
    label.textContent = labelText;
    const select = document.createElement('select');
    select.dataset.field = field;
    for (const value of values) select.add(new Option(value || 'Not specified', value));
    select.value = selected ?? '';
    label.append(select);
    return label;
  }

  function createEvaluationTurn(turn = {}) {
    const row = document.createElement('fieldset');
    row.className = 'evaluation-turn';
    row.append(makeField('Player question', 'question', turn.question ?? '', true));
    const expectations = document.createElement('div');
    expectations.className = 'evaluation-turn-grid';
    expectations.append(
      makeSelect('Expected scope', 'expectedScope', scopes, turn.expectedScope),
      makeSelect('Expected intent', 'expectedIntent', intents, turn.expectedIntent),
      makeField('Resolved entity', 'expectedEntity', turn.expectedEntity ?? ''),
      makeField('Source IDs (comma separated)', 'expectedSourceIds', (turn.expectedSourceIds ?? []).join(', ')),
      makeField('Required facts (comma separated)', 'requiredFacts', (turn.requiredFacts ?? []).join(', ')),
      makeField('Forbidden claims (comma separated)', 'forbiddenClaims', (turn.forbiddenClaims ?? []).join(', ')),
      makeField('Other expected properties (comma separated)', 'expectedProperties', (turn.expectedProperties ?? []).join(', '))
    );
    row.append(expectations);
    const controls = document.createElement('div');
    controls.className = 'evaluation-turn-controls';
    const moveUp = Object.assign(document.createElement('button'), { type: 'button', className: 'quiet-button', textContent: 'Move up' });
    const moveDown = Object.assign(document.createElement('button'), { type: 'button', className: 'quiet-button', textContent: 'Move down' });
    const remove = Object.assign(document.createElement('button'), { type: 'button', className: 'quiet-button', textContent: 'Remove turn' });
    moveUp.addEventListener('click', () => {
      const previous = row.previousElementSibling;
      if (previous) row.parentElement.insertBefore(row, previous);
    });
    moveDown.addEventListener('click', () => {
      const next = row.nextElementSibling;
      if (next) row.parentElement.insertBefore(next, row);
    });
    remove.addEventListener('click', () => {
      if (row.parentElement.children.length > 1) {
        row.remove();
        $('add-evaluation-turn').disabled = false;
      }
    });
    controls.append(moveUp, moveDown, remove);
    row.append(controls);
    return row;
  }

  function addEvaluationTurn(turn = {}) {
    const list = $('evaluation-turn-list');
    if (list.children.length >= 12) return;
    list.append(createEvaluationTurn(turn));
    $('add-evaluation-turn').disabled = list.children.length >= 12;
  }

  function resetEvaluationTurns(question) {
    $('evaluation-turn-list').replaceChildren();
    addEvaluationTurn({ question });
    $('add-evaluation-turn').disabled = false;
    $('evaluation-note').value = '';
  }

  function readEvaluationTurns() {
    return Array.from($('evaluation-turn-list').children).map(row => {
      const get = field => row.querySelector(`[data-field="${field}"]`).value.trim();
      const list = field => get(field).split(',').map(value => value.trim()).filter(Boolean);
      return {
        question: get('question'),
        expectedScope: get('expectedScope') || null,
        expectedIntent: get('expectedIntent') || null,
        expectedEntity: get('expectedEntity') || null,
        expectedSourceIds: list('expectedSourceIds'),
        requiredFacts: list('requiredFacts'),
        forbiddenClaims: list('forbiddenClaims'),
        expectedProperties: list('expectedProperties')
      };
    });
  }

  async function sendFeedback(event) {
    event.preventDefault();
    if (!state.selectedTrace) return;
    try {
      await api(`/admin/api/traces/${encodeURIComponent(state.selectedTrace.requestId)}/feedback?environment=${encodeURIComponent(state.selectedTrace.environment)}`, {
        method: 'POST', body: JSON.stringify({ label: $('feedback-label').value, note: $('feedback-note').value.trim() || null })
      });
      setMessage('Feedback saved. It does not alter routing or the active profile.', false, 'review-result');
      $('feedback-note').value = '';
      await loadTraces();
    } catch (error) {
      if (error.message !== 'unauthorized') setMessage(error.message, true, 'review-result');
    }
  }

  async function saveEvaluation(event) {
    event.preventDefault();
    if (!state.selectedTrace) return;
    try {
      const turns = readEvaluationTurns();
      if (turns.length < 1 || turns.some(turn => !turn.question))
        throw new Error('Each evaluation turn needs a question.');
      const first = turns[0];
      const result = await api(`/admin/api/evaluations?environment=${encodeURIComponent(state.selectedTrace.environment)}`, {
        method: 'POST', body: JSON.stringify({
          requestId: state.selectedTrace.requestId,
          expectedIntent: first.expectedIntent,
          expectedProperties: first.expectedProperties,
          turns,
          note: $('evaluation-note').value.trim() || null
        })
      });
      setMessage(`Reviewed evaluation case saved: ${result.exampleId}`, false, 'review-result');
      $('evaluation-note').value = '';
      await loadEvaluations();
    } catch (error) {
      if (error.message !== 'unauthorized') setMessage(error.message, true, 'review-result');
    }
  }

  $('token-form').addEventListener('submit', event => {
    event.preventDefault();
    const token = $('token-input').value.trim();
    if (token.length < 32) return;
    state.token = token;
    sessionStorage.setItem(tokenKey, token);
    $('token-input').value = '';
    dialog.close();
    refreshStatus();
  });
  $('cancel-token').addEventListener('click', () => dialog.close());
  $('logout').addEventListener('click', () => logout());
  $('refresh-status').addEventListener('click', refreshStatus);
  $('refresh-profiles').addEventListener('click', () => loadProfiles().catch(error => setMessage(error.message, true, 'profile-operation-result')));
  $('profile-select').addEventListener('change', () => loadProfile().catch(error => setMessage(error.message, true, 'profile-operation-result')));
  $('revision-select').addEventListener('change', () => {
    $('activate-draft').disabled = !state.profileDetails?.summary.enabled || selectedProfile() === 'Live' || !revisionIsDraft();
    $('promote-live').disabled = !state.profileDetails?.summary.enabled || selectedProfile() !== 'Dev' || !revisionIsAvailable();
    updateReplayButton();
  });
  $('replay-draft-revision').addEventListener('change', updateReplayButton);
  $('save-draft').addEventListener('click', saveDraft);
  $('validate-draft').addEventListener('click', validateDraft);
  $('activate-draft').addEventListener('click', activateRevision);
  $('promote-live').addEventListener('click', promoteRevision);
  $('rollback-profile').addEventListener('click', rollbackProfile);
  $('stage-bundle').addEventListener('click', stageBundle);
  $('refresh-evaluations').addEventListener('click', () => loadEvaluations().catch(error => setMessage(error.message, true, 'review-result')));
  $('add-evaluation-turn').addEventListener('click', () => addEvaluationTurn());
  $('run-replay').addEventListener('click', runReplay);
  $('feedback-form').addEventListener('submit', sendFeedback);
  $('evaluation-form').addEventListener('submit', saveEvaluation);
  $('refresh-traces').addEventListener('click', loadTraces);
  $('filters').addEventListener('submit', event => { event.preventDefault(); loadTraces(); });
  $('export-trace').addEventListener('click', exportTrace);
  if (state.token) refreshStatus(); else dialog.showModal();
})();
