<script lang="ts">
	import { onMount } from 'svelte';
	import { page } from '$app/stores';
	import { getApplications } from '$lib/api/applications';
	import { getApiKeys, createApiKey, revokeApiKey } from '$lib/api/apiKeys';
import { addRecipient, updateRecipient, createNotificationRule, updateNotificationRule, getRecipients, deleteRecipient, getNotificationRules, deleteNotificationRule } from '$lib/api/notifications';
	import Modal from '$lib/components/Modal.svelte';
	import ConfirmDialog from '$lib/components/ConfirmDialog.svelte';
	import { toasts } from '$lib/stores/toast';
	import { Plus, Key, Bell, LayoutGrid, Copy, Check, ArrowLeft, Bug } from '@lucide/svelte';
	import type { Application, ApiKey, CreatedApiKey, NotificationRecipient, NotificationRule } from '$lib/types';

	let appId = $derived($page.params.id);
	let app = $state<Application | null>(null);
	let loading = $state(true);
	let activeTab = $state<'overview' | 'apikeys' | 'notifications'>('overview');

	// API Key state
	let showKeyModal = $state(false);
	let createdKey = $state<CreatedApiKey | null>(null);
	let keyCopied = $state(false);
	let keyForm = $state({ name: '', retentionDays: 90, rateLimitPerMinute: 1500, allowAllIps: true, ipAllowlist: '' });
	let savingKey = $state(false);
	let revokeTarget = $state<string | null>(null);
	let apiKeys = $state<ApiKey[]>([]);
	let loadingKeys = $state(false);

	// Recipient state
	let showRecipientModal = $state(false);
	let recipientForm = $state({ type: 'Email' as 'Email' | 'Webhook', name: '', email: '', webhookUrl: '', webhookSecret: '' });
	let savingRecipient = $state(false);
	let recipients = $state<NotificationRecipient[]>([]);
	let loadingRecipients = $state(false);
	let deleteRecipientTarget = $state<string | null>(null);
	let editingRecipientId = $state<string | null>(null);

	// Notification rule state
	let showRuleModal = $state(false);
	let ruleForm = $state({ name: '', environment: '', eventType: 'FirstSeen', severityMinimum: 'Error', cooldownMinutes: 60, thresholdCount: 10, thresholdWindowMinutes: 5, digestIntervalMinutes: 60 });
	let savingRule = $state(false);
	let rules = $state<NotificationRule[]>([]);
	let loadingRules = $state(false);
	let deleteRuleTarget = $state<string | null>(null);
	let editingRuleId = $state<string | null>(null);

	onMount(async () => {
		loading = true;
		try {
			const apps = await getApplications();
			app = apps.find((a) => a.id === appId) ?? null;
		} catch (e: any) {
			toasts.error(e.message ?? 'Failed to load application');
		} finally {
			loading = false;
		}
	});

	async function loadApiKeys() {
		loadingKeys = true;
		try {
			apiKeys = await getApiKeys(appId);
		} catch (e: any) {
			toasts.error(e.message ?? 'Failed to load API keys');
		} finally {
			loadingKeys = false;
		}
	}

	$effect(() => {
		if (activeTab === 'apikeys') loadApiKeys();
		if (activeTab === 'notifications') { loadRecipients(); loadRules(); }
	});

	async function loadRecipients() {
		loadingRecipients = true;
		try {
			recipients = await getRecipients(appId);
		} catch (e: any) {
			toasts.error(e.message ?? 'Failed to load recipients');
		} finally {
			loadingRecipients = false;
		}
	}

	async function loadRules() {
		loadingRules = true;
		try {
			rules = await getNotificationRules(appId);
		} catch (e: any) {
			toasts.error(e.message ?? 'Failed to load notification rules');
		} finally {
			loadingRules = false;
		}
	}

	async function submitDeleteRecipient() {
		if (!deleteRecipientTarget) return;
		try {
			await deleteRecipient(deleteRecipientTarget);
			toasts.success('Recipient removed');
			deleteRecipientTarget = null;
			await loadRecipients();
		} catch (e: any) {
			toasts.error(e.message ?? 'Failed to remove recipient');
		}
	}

	async function submitDeleteRule() {
		if (!deleteRuleTarget) return;
		try {
			await deleteNotificationRule(deleteRuleTarget);
			toasts.success('Rule removed');
			deleteRuleTarget = null;
			await loadRules();
		} catch (e: any) {
			toasts.error(e.message ?? 'Failed to remove rule');
		}
	}

	async function submitCreateKey() {
		if (!keyForm.name.trim()) return;
		savingKey = true;
		try {
			const result = await createApiKey({
				applicationId: appId,
				name: keyForm.name.trim(),
				retentionDays: keyForm.retentionDays,
				rateLimitPerMinute: keyForm.rateLimitPerMinute,
				allowAllIps: keyForm.allowAllIps,
				ipAllowlist: keyForm.allowAllIps ? [] : keyForm.ipAllowlist.split('\n').map((s) => s.trim()).filter(Boolean)
			});
			createdKey = result;
			showKeyModal = false;
			keyForm = { name: '', retentionDays: 90, rateLimitPerMinute: 1500, allowAllIps: true, ipAllowlist: '' };
			await loadApiKeys();
		} catch (e: any) {
			toasts.error(e.message ?? 'Failed to create API key');
		} finally {
			savingKey = false;
		}
	}

	async function submitRevoke() {
		if (!revokeTarget) return;
		try {
			await revokeApiKey(revokeTarget);
			toasts.success('API key revoked');
			revokeTarget = null;
			await loadApiKeys();
		} catch (e: any) {
			toasts.error(e.message ?? 'Failed to revoke key');
		}
	}


	function openEditRecipient(r: NotificationRecipient) {
		editingRecipientId = r.id;
		recipientForm = { type: r.type, name: r.name, email: r.email ?? '', webhookUrl: r.webhookUrl ?? '', webhookSecret: '' };
		showRecipientModal = true;
	}

	function openEditRule(rule: NotificationRule) {
		editingRuleId = rule.id;
		ruleForm = {
			name: rule.name,
			environment: rule.environment ?? '',
			eventType: rule.eventType,
			severityMinimum: rule.severityMinimum,
			cooldownMinutes: rule.cooldownMinutes,
			thresholdCount: rule.thresholdCount ?? 10,
			thresholdWindowMinutes: rule.thresholdWindowMinutes ?? 5,
			digestIntervalMinutes: rule.digestIntervalMinutes ?? 60
		};
		showRuleModal = true;
	}

	async function submitAddRecipient() {
		if (!recipientForm.name.trim()) return;
		savingRecipient = true;
		try {
			if (editingRecipientId) {
				await updateRecipient(editingRecipientId, {
					name: recipientForm.name.trim(),
					email: recipientForm.type === 'Email' ? recipientForm.email.trim() : undefined,
					webhookUrl: recipientForm.type === 'Webhook' ? recipientForm.webhookUrl.trim() : undefined,
					webhookSecret: recipientForm.webhookSecret.trim() || undefined
				});
				toasts.success('Recipient updated');
			} else {
				await addRecipient({
					applicationId: appId,
					type: recipientForm.type,
					name: recipientForm.name.trim(),
					email: recipientForm.type === 'Email' ? recipientForm.email.trim() : undefined,
					webhookUrl: recipientForm.type === 'Webhook' ? recipientForm.webhookUrl.trim() : undefined,
					webhookSecret: recipientForm.webhookSecret.trim() || undefined
				});
				toasts.success('Recipient added');
			}
			showRecipientModal = false;
			editingRecipientId = null;
			recipientForm = { type: 'Email', name: '', email: '', webhookUrl: '', webhookSecret: '' };
			await loadRecipients();
		} catch (e: any) {
			toasts.error(e.message ?? 'Failed to save recipient');
		} finally {
			savingRecipient = false;
		}
	}

	async function submitCreateRule() {
		if (!ruleForm.name.trim()) return;
		savingRule = true;
		try {
			const payload = {
				name: ruleForm.name.trim(),
				environment: ruleForm.environment.trim() || undefined,
				eventType: ruleForm.eventType,
				severityMinimum: ruleForm.severityMinimum,
				cooldownMinutes: ruleForm.cooldownMinutes,
				thresholdCount: ruleForm.eventType === 'Threshold' ? ruleForm.thresholdCount : undefined,
				thresholdWindowMinutes: ruleForm.eventType === 'Threshold' ? ruleForm.thresholdWindowMinutes : undefined,
				digestIntervalMinutes: ruleForm.eventType === 'Digest' ? ruleForm.digestIntervalMinutes : undefined
			};
			if (editingRuleId) {
				await updateNotificationRule(editingRuleId, payload);
				toasts.success('Rule updated');
			} else {
				await createNotificationRule({ applicationId: appId, ...payload });
				toasts.success('Notification rule created');
			}
			showRuleModal = false;
			editingRuleId = null;
			await loadRules();
		} catch (e: any) {
			toasts.error(e.message ?? 'Failed to save rule');
		} finally {
			savingRule = false;
		}
	}

	async function copyKey() {
		if (!createdKey) return;
		await navigator.clipboard.writeText(createdKey.plaintextKey);
		keyCopied = true;
		setTimeout(() => (keyCopied = false), 2000);
	}

	const tabs = [
		{ id: 'overview', label: 'Overview', icon: LayoutGrid },
		{ id: 'apikeys', label: 'API Keys', icon: Key },
		{ id: 'notifications', label: 'Notifications', icon: Bell }
	] as const;

	const severities = ['Debug', 'Info', 'Warning', 'Error', 'Critical'];
	const eventTypes = ['FirstSeen', 'Regression', 'Threshold', 'Digest'];
</script>

<svelte:head><title>{app?.name ?? 'Application'} — Exception Monitor</title></svelte:head>

<div class="p-6">
	<a href="/clients/{app?.clientId}" class="inline-flex items-center gap-1 text-sm text-gray-500 hover:text-gray-700 mb-4">
		<ArrowLeft size={14} /> {app?.clientName ?? 'Back'}
	</a>

	{#if loading}
		<div class="flex items-center gap-2 text-gray-500 text-sm">
			<div class="animate-spin h-4 w-4 border-2 border-indigo-600 border-t-transparent rounded-full"></div>
			Loading...
		</div>
	{:else if !app}
		<p class="text-gray-500">Application not found.</p>
	{:else}
		<div class="mb-6 flex items-start justify-between gap-4">
			<div>
				<h1 class="text-2xl font-bold text-gray-900">{app.name}</h1>
				<p class="text-sm text-gray-500 font-mono mt-0.5">{app.slug}</p>
			</div>
			<a href="/apps/{appId}/errors" class="shrink-0 inline-flex items-center gap-1.5 px-4 py-2 text-sm font-medium text-white bg-indigo-600 rounded-lg hover:bg-indigo-700 transition-colors">
				<Bug size={14} /> View Errors
			</a>
		</div>

		<!-- Tabs -->
		<div class="border-b border-gray-200 mb-6">
			<nav class="-mb-px flex gap-6">
				{#each tabs as tab}
					{@const TabIcon = tab.icon}
					<button
						onclick={() => (activeTab = tab.id)}
						class="flex items-center gap-2 pb-3 text-sm font-medium border-b-2 transition-colors
						{activeTab === tab.id
							? 'border-indigo-600 text-indigo-600'
							: 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300'}"
					>
						<TabIcon size={15} />
						{tab.label}
					</button>
				{/each}
			</nav>
		</div>

		<!-- Overview Tab -->
		{#if activeTab === 'overview'}
			<div class="bg-white rounded-xl border border-gray-200 p-5 max-w-lg">
				<h3 class="font-semibold text-gray-800 mb-4">Application Info</h3>
				<dl class="space-y-3 text-sm">
					<div class="flex justify-between"><dt class="text-gray-500">Client</dt><dd class="font-medium">{app.clientName}</dd></div>
					<div class="flex justify-between"><dt class="text-gray-500">Default Environment</dt><dd class="font-medium">{app.defaultEnvironment}</dd></div>
					<div class="flex justify-between"><dt class="text-gray-500">Retention</dt><dd class="font-medium">{app.defaultRetentionDays} days</dd></div>
					<div class="flex justify-between"><dt class="text-gray-500">Status</dt>
						<dd>
							<span class="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium {app.isActive ? 'bg-green-100 text-green-700' : 'bg-gray-100 text-gray-500'}">
								{app.isActive ? 'Active' : 'Inactive'}
							</span>
						</dd>
					</div>
					<div class="flex justify-between"><dt class="text-gray-500">Created</dt><dd class="font-medium">{new Date(app.createdAt).toLocaleDateString()}</dd></div>
				</dl>
			</div>

		<!-- API Keys Tab -->
		{:else if activeTab === 'apikeys'}
			{#if createdKey}
				<div class="mb-6 bg-green-50 border border-green-200 rounded-xl p-5">
					<div class="flex items-start justify-between mb-2">
						<div>
							<p class="font-semibold text-green-800">API Key Created: {createdKey.name}</p>
							<p class="text-sm text-green-700 mt-0.5">Copy this key now — it will not be shown again.</p>
						</div>
						<button onclick={() => (createdKey = null)} class="text-green-400 hover:text-green-600 text-lg leading-none">&times;</button>
					</div>
					<div class="flex items-center gap-3 mt-3">
						<code class="flex-1 bg-white border border-green-200 rounded px-3 py-2 text-sm font-mono break-all">{createdKey.plaintextKey}</code>
						<button onclick={copyKey} class="flex items-center gap-1.5 px-3 py-2 bg-green-600 text-white text-sm rounded-md hover:bg-green-700 shrink-0">
							{#if keyCopied}<Check size={14} /> Copied{:else}<Copy size={14} /> Copy{/if}
						</button>
					</div>
				</div>
			{/if}

			<div class="flex justify-between items-center mb-4">
				<p class="text-sm text-gray-500">Create and manage API keys for this application.</p>
				<button onclick={() => (showKeyModal = true)} class="flex items-center gap-2 px-4 py-2 bg-indigo-600 text-white text-sm font-medium rounded-md hover:bg-indigo-700">
					<Plus size={16} /> Create API Key
				</button>
			</div>

			<div class="bg-amber-50 border border-amber-200 rounded-lg p-4 text-sm text-amber-800 mb-5">
				API keys are used by your application to post errors to <code class="bg-amber-100 px-1 rounded">POST /api/ingest</code> using the <code class="bg-amber-100 px-1 rounded">X-Exception-Api-Key</code> header.
			</div>

			{#if loadingKeys}
				<div class="flex items-center gap-2 text-gray-400 text-sm">
					<div class="animate-spin h-4 w-4 border-2 border-indigo-500 border-t-transparent rounded-full"></div>
					Loading keys…
				</div>
			{:else if apiKeys.length === 0}
				<p class="text-sm text-gray-400">No API keys yet.</p>
			{:else}
				<div class="bg-white rounded-xl border border-gray-200 overflow-hidden">
					<table class="w-full text-sm">
						<thead>
							<tr class="border-b border-gray-100 bg-gray-50">
								<th class="text-left px-5 py-3 text-xs font-medium text-gray-500 uppercase tracking-wide">Name</th>
								<th class="text-left px-5 py-3 text-xs font-medium text-gray-500 uppercase tracking-wide">Key</th>
								<th class="text-left px-4 py-3 text-xs font-medium text-gray-500 uppercase tracking-wide">Status</th>
								<th class="text-left px-4 py-3 text-xs font-medium text-gray-500 uppercase tracking-wide">Last Used</th>
								<th class="text-left px-4 py-3 text-xs font-medium text-gray-500 uppercase tracking-wide">Created</th>
								<th class="px-4 py-3"></th>
							</tr>
						</thead>
						<tbody class="divide-y divide-gray-50">
							{#each apiKeys as key}
								<tr class="hover:bg-gray-50 transition-colors">
									<td class="px-5 py-3.5 font-medium text-gray-800">{key.name}</td>
									<td class="px-5 py-3.5">
										<code class="text-xs font-mono text-gray-600 bg-gray-100 px-2 py-1 rounded">
											exm_{key.keyPrefix}_•••••••••••••••
										</code>
									</td>
									<td class="px-4 py-3.5">
										{#if key.isActive}
											<span class="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-green-100 text-green-700">Active</span>
										{:else}
											<span class="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-gray-100 text-gray-500">Revoked</span>
										{/if}
									</td>
									<td class="px-4 py-3.5 text-gray-500 text-xs">
										{key.lastUsedAt ? new Date(key.lastUsedAt).toLocaleString() : '—'}
									</td>
									<td class="px-4 py-3.5 text-gray-400 text-xs">
										{new Date(key.createdAt).toLocaleDateString()}
									</td>
									<td class="px-4 py-3.5 text-right">
										{#if key.isActive}
											<button
												onclick={() => (revokeTarget = key.id)}
												class="text-xs text-red-500 hover:text-red-700 font-medium"
											>Revoke</button>
										{/if}
									</td>
								</tr>
							{/each}
						</tbody>
					</table>
				</div>
			{/if}

		<!-- Notifications Tab -->
		{:else if activeTab === 'notifications'}
			<div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
				<!-- Recipients -->
				<div class="bg-white rounded-xl border border-gray-200 p-5">
					<div class="flex items-center justify-between mb-4">
						<h3 class="font-semibold text-gray-800">Recipients</h3>
						<button onclick={() => (showRecipientModal = true)} class="flex items-center gap-1 text-sm text-indigo-600 hover:text-indigo-800 font-medium">
							<Plus size={14} /> Add
						</button>
					</div>
					{#if loadingRecipients}
						<p class="text-sm text-gray-400">Loading…</p>
					{:else if recipients.length === 0}
						<p class="text-sm text-gray-400">No recipients yet. Add an email or webhook to receive alerts.</p>
					{:else}
						<ul class="divide-y divide-gray-100">
							{#each recipients as r}
								<li class="flex items-center justify-between py-2.5 gap-3">
									<div class="min-w-0">
										<p class="text-sm font-medium text-gray-800 truncate">{r.name}</p>
										<p class="text-xs text-gray-500 truncate">{r.type === 'Email' ? r.email : r.webhookUrl}</p>
									</div>
									<div class="flex items-center gap-2 shrink-0">
										<span class="text-xs px-1.5 py-0.5 rounded bg-gray-100 text-gray-600">{r.type}</span>
										{#if r.isActive}
											<button onclick={() => openEditRecipient(r)} class="text-xs text-indigo-600 hover:text-indigo-800 font-medium">Edit</button>
											<button onclick={() => (deleteRecipientTarget = r.id)} class="text-xs text-red-500 hover:text-red-700 font-medium">Remove</button>
										{:else}
											<span class="text-xs text-gray-400 italic">Inactive</span>
										{/if}
									</div>
								</li>
							{/each}
						</ul>
					{/if}
				</div>

				<!-- Notification Rules -->
				<div class="bg-white rounded-xl border border-gray-200 p-5">
					<div class="flex items-center justify-between mb-4">
						<h3 class="font-semibold text-gray-800">Notification Rules</h3>
						<button onclick={() => (showRuleModal = true)} class="flex items-center gap-1 text-sm text-indigo-600 hover:text-indigo-800 font-medium">
							<Plus size={14} /> Add
						</button>
					</div>
					{#if loadingRules}
						<p class="text-sm text-gray-400">Loading…</p>
					{:else if rules.length === 0}
						<p class="text-sm text-gray-400">No rules yet. Define when alerts should fire.</p>
					{:else}
						<ul class="divide-y divide-gray-100">
							{#each rules as rule}
								<li class="flex items-center justify-between py-2.5 gap-3">
									<div class="min-w-0">
										<p class="text-sm font-medium text-gray-800 truncate">{rule.name}</p>
										<p class="text-xs text-gray-500 truncate">
											{rule.eventType}
											{#if rule.environment} · {rule.environment}{/if}
											· min severity: {rule.severityMinimum}
										</p>
									</div>
									<div class="flex items-center gap-2 shrink-0">
										{#if rule.isActive}
											<button onclick={() => openEditRule(rule)} class="text-xs text-indigo-600 hover:text-indigo-800 font-medium">Edit</button>
											<button onclick={() => (deleteRuleTarget = rule.id)} class="text-xs text-red-500 hover:text-red-700 font-medium">Remove</button>
										{:else}
											<span class="text-xs text-gray-400 italic">Inactive</span>
										{/if}
									</div>
								</li>
							{/each}
						</ul>
					{/if}
				</div>
			</div>
		{/if}
	{/if}
</div>

<!-- Create API Key Modal -->
<Modal title="Create API Key" open={showKeyModal} onClose={() => (showKeyModal = false)}>
	<form onsubmit={(e) => { e.preventDefault(); submitCreateKey(); }}>
		<div class="space-y-4">
			<div>
				<label class="block text-sm font-medium text-gray-700 mb-1">Name <span class="text-red-500">*</span></label>
				<input bind:value={keyForm.name} required placeholder="Production Key" class="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500" />
			</div>
			<div class="grid grid-cols-2 gap-4">
				<div>
					<label class="block text-sm font-medium text-gray-700 mb-1">Rate Limit / min</label>
					<input type="number" bind:value={keyForm.rateLimitPerMinute} min="1" class="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500" />
				</div>
				<div>
					<label class="block text-sm font-medium text-gray-700 mb-1">Retention (days)</label>
					<input type="number" bind:value={keyForm.retentionDays} min="1" max="3650" class="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500" />
				</div>
			</div>
			<label class="flex items-center gap-2 text-sm text-gray-600 cursor-pointer">
				<input type="checkbox" bind:checked={keyForm.allowAllIps} class="rounded" />
				Allow all IP addresses
			</label>
			{#if !keyForm.allowAllIps}
				<div>
					<label class="block text-sm font-medium text-gray-700 mb-1">IP Allowlist (CIDR, one per line)</label>
					<textarea bind:value={keyForm.ipAllowlist} rows="3" placeholder="192.168.1.0/24" class="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 font-mono"></textarea>
				</div>
			{/if}
			<div class="flex justify-end gap-3 pt-1">
				<button type="button" onclick={() => (showKeyModal = false)} class="px-4 py-2 text-sm text-gray-700 border border-gray-300 rounded-md hover:bg-gray-50">Cancel</button>
				<button type="submit" disabled={savingKey || !keyForm.name.trim()} class="px-4 py-2 text-sm text-white bg-indigo-600 rounded-md hover:bg-indigo-700 disabled:opacity-50">
					{savingKey ? 'Creating...' : 'Create Key'}
				</button>
			</div>
		</div>
	</form>
</Modal>

<!-- Revoke Confirm -->
<ConfirmDialog
	open={revokeTarget !== null}
	title="Revoke API Key"
	message="This key will immediately stop working. This cannot be undone."
	confirmLabel="Revoke"
	onConfirm={submitRevoke}
	onCancel={() => (revokeTarget = null)}
/>

<!-- Add / Edit Recipient Modal -->
<Modal title={editingRecipientId ? 'Edit Recipient' : 'Add Recipient'} open={showRecipientModal} onClose={() => { showRecipientModal = false; editingRecipientId = null; recipientForm = { type: 'Email', name: '', email: '', webhookUrl: '', webhookSecret: '' }; }}>
	<form onsubmit={(e) => { e.preventDefault(); submitAddRecipient(); }}>
		<div class="space-y-4">
			<div>
				<label class="block text-sm font-medium text-gray-700 mb-1">Type</label>
				<select bind:value={recipientForm.type} disabled={!!editingRecipientId} class="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 disabled:bg-gray-50 disabled:text-gray-500">
					<option value="Email">Email</option>
					<option value="Webhook">Webhook</option>
				</select>
			</div>
			<div>
				<label class="block text-sm font-medium text-gray-700 mb-1">Name <span class="text-red-500">*</span></label>
				<input bind:value={recipientForm.name} required placeholder="Ops Team" class="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500" />
			</div>
			{#if recipientForm.type === 'Email'}
				<div>
					<label class="block text-sm font-medium text-gray-700 mb-1">Email Address <span class="text-red-500">*</span></label>
					<input type="email" bind:value={recipientForm.email} required class="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500" />
				</div>
			{:else}
				<div>
					<label class="block text-sm font-medium text-gray-700 mb-1">Webhook URL <span class="text-red-500">*</span></label>
					<input type="url" bind:value={recipientForm.webhookUrl} required placeholder="https://..." class="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500" />
				</div>
				<div>
					<label class="block text-sm font-medium text-gray-700 mb-1">Webhook Secret <span class="text-gray-400 text-xs">(optional)</span></label>
					<input bind:value={recipientForm.webhookSecret} class="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500" />
				</div>
			{/if}
			<div class="flex justify-end gap-3 pt-1">
				<button type="button" onclick={() => { showRecipientModal = false; editingRecipientId = null; recipientForm = { type: 'Email', name: '', email: '', webhookUrl: '', webhookSecret: '' }; }} class="px-4 py-2 text-sm text-gray-700 border border-gray-300 rounded-md hover:bg-gray-50">Cancel</button>
				<button type="submit" disabled={savingRecipient} class="px-4 py-2 text-sm text-white bg-indigo-600 rounded-md hover:bg-indigo-700 disabled:opacity-50">
					{savingRecipient ? 'Saving...' : editingRecipientId ? 'Save Changes' : 'Add Recipient'}
				</button>
			</div>
		</div>
	</form>
</Modal>

<!-- Create / Edit Rule Modal -->
<Modal title={editingRuleId ? 'Edit Notification Rule' : 'Create Notification Rule'} open={showRuleModal} onClose={() => { showRuleModal = false; editingRuleId = null; }}>
	<form onsubmit={(e) => { e.preventDefault(); submitCreateRule(); }}>
		<div class="space-y-4">
			<div>
				<label class="block text-sm font-medium text-gray-700 mb-1">Rule Name <span class="text-red-500">*</span></label>
				<input bind:value={ruleForm.name} required placeholder="New Errors Alert" class="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500" />
			</div>
			<div class="grid grid-cols-2 gap-4">
				<div>
					<label class="block text-sm font-medium text-gray-700 mb-1">Event Type</label>
					<select bind:value={ruleForm.eventType} class="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500">
						{#each eventTypes as t}<option value={t}>{t}</option>{/each}
					</select>
				</div>
				<div>
					<label class="block text-sm font-medium text-gray-700 mb-1">Min Severity</label>
					<select bind:value={ruleForm.severityMinimum} class="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500">
						{#each severities as s}<option value={s}>{s}</option>{/each}
					</select>
				</div>
			</div>
			<div class="grid grid-cols-2 gap-4">
				<div>
					<label class="block text-sm font-medium text-gray-700 mb-1">Environment <span class="text-gray-400 text-xs">(optional)</span></label>
					<input bind:value={ruleForm.environment} placeholder="production" class="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500" />
				</div>
				<div>
					<label class="block text-sm font-medium text-gray-700 mb-1">Cooldown (mins)</label>
					<input type="number" bind:value={ruleForm.cooldownMinutes} min="0" class="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500" />
				</div>
			</div>
			{#if ruleForm.eventType === 'Threshold'}
				<div class="grid grid-cols-2 gap-4">
					<div>
						<label class="block text-sm font-medium text-gray-700 mb-1">Threshold Count</label>
						<input type="number" bind:value={ruleForm.thresholdCount} min="1" class="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500" />
					</div>
					<div>
						<label class="block text-sm font-medium text-gray-700 mb-1">Window (mins)</label>
						<input type="number" bind:value={ruleForm.thresholdWindowMinutes} min="1" class="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500" />
					</div>
				</div>
			{/if}
			{#if ruleForm.eventType === 'Digest'}
				<div>
					<label class="block text-sm font-medium text-gray-700 mb-1">Digest Interval (mins)</label>
					<input type="number" bind:value={ruleForm.digestIntervalMinutes} min="1" class="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500" />
				</div>
			{/if}
			<div class="flex justify-end gap-3 pt-1">
				<button type="button" onclick={() => { showRuleModal = false; editingRuleId = null; }} class="px-4 py-2 text-sm text-gray-700 border border-gray-300 rounded-md hover:bg-gray-50">Cancel</button>
				<button type="submit" disabled={savingRule} class="px-4 py-2 text-sm text-white bg-indigo-600 rounded-md hover:bg-indigo-700 disabled:opacity-50">
					{savingRule ? 'Saving...' : editingRuleId ? 'Save Changes' : 'Create Rule'}
				</button>
			</div>
		</div>
	</form>
</Modal>

<!-- Remove Recipient Confirm -->
<ConfirmDialog
	open={deleteRecipientTarget !== null}
	title="Remove Recipient"
	message="This recipient will stop receiving notifications. This cannot be undone."
	confirmLabel="Remove"
	onConfirm={submitDeleteRecipient}
	onCancel={() => (deleteRecipientTarget = null)}
/>

<!-- Remove Rule Confirm -->
<ConfirmDialog
	open={deleteRuleTarget !== null}
	title="Remove Rule"
	message="This notification rule will be disabled and no longer trigger alerts."
	confirmLabel="Remove"
	onConfirm={submitDeleteRule}
	onCancel={() => (deleteRuleTarget = null)}
/>
