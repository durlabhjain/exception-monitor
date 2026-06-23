<script lang="ts">
	import { onMount } from 'svelte';
	import { getUsers, createUser, grantClientAccess, grantApplicationAccess } from '$lib/api/users';
	import { getClients } from '$lib/api/clients';
	import { getApplications } from '$lib/api/applications';
	import Modal from '$lib/components/Modal.svelte';
	import { toasts } from '$lib/stores/toast';
	import { Plus, UserCircle, Shield } from '@lucide/svelte';
	import type { User, Client, Application } from '$lib/types';

	let users = $state<User[]>([]);
	let clients = $state<Client[]>([]);
	let applications = $state<Application[]>([]);
	let loading = $state(true);

	let showUserModal = $state(false);
	let showClientAccessModal = $state(false);
	let showAppAccessModal = $state(false);
	let selectedUserId = $state('');

	let userForm = $state({ email: '', displayName: '' });
	let clientAccessForm = $state({ clientId: '', role: 'Developer', allApplications: true });
	let appAccessForm = $state({ applicationId: '', role: 'Developer' });

	let saving = $state(false);

	const clientRoles = ['SystemAdmin', 'ClientAdmin', 'AppAdmin', 'Developer', 'Viewer'];
	const appRoles = ['AppAdmin', 'Developer', 'Viewer'];

	onMount(async () => {
		loading = true;
		try {
			const [u, c, a] = await Promise.all([getUsers(), getClients(), getApplications()]);
			users = u;
			clients = c;
			applications = a;
		} catch (e: any) {
			toasts.error(e.message ?? 'Failed to load users');
		} finally {
			loading = false;
		}
	});

	async function submitCreateUser() {
		if (!userForm.email.trim()) return;
		saving = true;
		try {
			const user = await createUser({ email: userForm.email.trim(), displayName: userForm.displayName.trim() || undefined });
			users = [...users, user];
			toasts.success(`User ${user.email} created`);
			showUserModal = false;
			userForm = { email: '', displayName: '' };
		} catch (e: any) {
			toasts.error(e.message ?? 'Failed to create user');
		} finally {
			saving = false;
		}
	}

	async function submitGrantClientAccess() {
		if (!selectedUserId || !clientAccessForm.clientId) return;
		saving = true;
		try {
			await grantClientAccess({ userId: selectedUserId, clientId: clientAccessForm.clientId, role: clientAccessForm.role, allApplications: clientAccessForm.allApplications });
			toasts.success('Client access granted');
			showClientAccessModal = false;
		} catch (e: any) {
			toasts.error(e.message ?? 'Failed to grant access');
		} finally {
			saving = false;
		}
	}

	async function submitGrantAppAccess() {
		if (!selectedUserId || !appAccessForm.applicationId) return;
		saving = true;
		try {
			await grantApplicationAccess({ userId: selectedUserId, applicationId: appAccessForm.applicationId, role: appAccessForm.role });
			toasts.success('Application access granted');
			showAppAccessModal = false;
		} catch (e: any) {
			toasts.error(e.message ?? 'Failed to grant access');
		} finally {
			saving = false;
		}
	}

	function openClientAccess(userId: string) {
		selectedUserId = userId;
		clientAccessForm = { clientId: clients[0]?.id ?? '', role: 'Developer', allApplications: true };
		showClientAccessModal = true;
	}

	function openAppAccess(userId: string) {
		selectedUserId = userId;
		appAccessForm = { applicationId: applications[0]?.id ?? '', role: 'Developer' };
		showAppAccessModal = true;
	}
</script>

<svelte:head><title>Users — Exception Monitor</title></svelte:head>

<div class="p-6">
	<div class="flex items-center justify-between mb-6">
		<div>
			<h1 class="text-2xl font-bold text-gray-900">Users</h1>
			<p class="text-sm text-gray-500 mt-0.5">Manage users and their access permissions</p>
		</div>
		<button
			onclick={() => (showUserModal = true)}
			class="flex items-center gap-2 px-4 py-2 bg-indigo-600 text-white text-sm font-medium rounded-md hover:bg-indigo-700"
		>
			<Plus size={16} /> New User
		</button>
	</div>

	{#if loading}
		<div class="flex items-center gap-2 text-gray-500 text-sm">
			<div class="animate-spin h-4 w-4 border-2 border-indigo-600 border-t-transparent rounded-full"></div>
			Loading...
		</div>
	{:else if users.length === 0}
		<div class="bg-white rounded-xl border border-dashed border-gray-300 py-20 text-center">
			<UserCircle size={36} class="text-gray-300 mx-auto mb-3" />
			<p class="text-gray-500 font-medium">No users yet</p>
			<button onclick={() => (showUserModal = true)} class="mt-4 px-4 py-2 bg-indigo-600 text-white text-sm font-medium rounded-md hover:bg-indigo-700">
				New User
			</button>
		</div>
	{:else}
		<div class="bg-white rounded-xl border border-gray-200 overflow-hidden">
			<table class="w-full text-sm">
				<thead>
					<tr class="border-b border-gray-100 bg-gray-50/50">
						<th class="text-left px-5 py-3 text-xs font-medium text-gray-500 uppercase tracking-wide">User</th>
						<th class="text-left px-5 py-3 text-xs font-medium text-gray-500 uppercase tracking-wide">Status</th>
						<th class="text-right px-5 py-3 text-xs font-medium text-gray-500 uppercase tracking-wide">Access</th>
					</tr>
				</thead>
				<tbody class="divide-y divide-gray-50">
					{#each users as user}
						<tr class="hover:bg-gray-50 transition-colors">
							<td class="px-5 py-3.5">
								<p class="font-medium text-gray-900">{user.displayName ?? user.email}</p>
								{#if user.displayName}<p class="text-gray-400 text-xs">{user.email}</p>{/if}
							</td>
							<td class="px-5 py-3.5">
								<span class="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium
								{user.isActive ? 'bg-green-100 text-green-700' : 'bg-gray-100 text-gray-500'}">
									{user.isActive ? 'Active' : 'Inactive'}
								</span>
							</td>
							<td class="px-5 py-3.5 text-right">
								<div class="flex items-center justify-end gap-2">
									<button
										onclick={() => openClientAccess(user.id)}
										class="flex items-center gap-1 text-xs text-indigo-600 hover:text-indigo-800 font-medium px-2 py-1 rounded hover:bg-indigo-50"
									>
										<Shield size={12} /> Client Access
									</button>
									<button
										onclick={() => openAppAccess(user.id)}
										class="flex items-center gap-1 text-xs text-indigo-600 hover:text-indigo-800 font-medium px-2 py-1 rounded hover:bg-indigo-50"
									>
										<Shield size={12} /> App Access
									</button>
								</div>
							</td>
						</tr>
					{/each}
				</tbody>
			</table>
		</div>
	{/if}
</div>

<!-- Create User Modal -->
<Modal title="New User" open={showUserModal} onClose={() => (showUserModal = false)}>
	<form onsubmit={(e) => { e.preventDefault(); submitCreateUser(); }}>
		<div class="space-y-4">
			<div>
				<label class="block text-sm font-medium text-gray-700 mb-1">Email <span class="text-red-500">*</span></label>
				<input type="email" bind:value={userForm.email} required placeholder="user@example.com" class="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500" />
			</div>
			<div>
				<label class="block text-sm font-medium text-gray-700 mb-1">Display Name <span class="text-gray-400 text-xs">(optional)</span></label>
				<input bind:value={userForm.displayName} placeholder="John Doe" class="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500" />
			</div>
			<div class="flex justify-end gap-3 pt-1">
				<button type="button" onclick={() => (showUserModal = false)} class="px-4 py-2 text-sm text-gray-700 border border-gray-300 rounded-md hover:bg-gray-50">Cancel</button>
				<button type="submit" disabled={saving || !userForm.email.trim()} class="px-4 py-2 text-sm text-white bg-indigo-600 rounded-md hover:bg-indigo-700 disabled:opacity-50">
					{saving ? 'Creating...' : 'Create User'}
				</button>
			</div>
		</div>
	</form>
</Modal>

<!-- Grant Client Access Modal -->
<Modal title="Grant Client Access" open={showClientAccessModal} onClose={() => (showClientAccessModal = false)}>
	<form onsubmit={(e) => { e.preventDefault(); submitGrantClientAccess(); }}>
		<div class="space-y-4">
			<div>
				<label class="block text-sm font-medium text-gray-700 mb-1">Client</label>
				<select bind:value={clientAccessForm.clientId} class="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 bg-white">
					{#each clients as c}<option value={c.id}>{c.name}</option>{/each}
				</select>
			</div>
			<div>
				<label class="block text-sm font-medium text-gray-700 mb-1">Role</label>
				<select bind:value={clientAccessForm.role} class="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 bg-white">
					{#each clientRoles as r}<option value={r}>{r}</option>{/each}
				</select>
			</div>
			<label class="flex items-center gap-2 text-sm text-gray-600 cursor-pointer">
				<input type="checkbox" bind:checked={clientAccessForm.allApplications} class="rounded" />
				Access to all applications under this client
			</label>
			<div class="flex justify-end gap-3 pt-1">
				<button type="button" onclick={() => (showClientAccessModal = false)} class="px-4 py-2 text-sm text-gray-700 border border-gray-300 rounded-md hover:bg-gray-50">Cancel</button>
				<button type="submit" disabled={saving} class="px-4 py-2 text-sm text-white bg-indigo-600 rounded-md hover:bg-indigo-700 disabled:opacity-50">
					{saving ? 'Granting...' : 'Grant Access'}
				</button>
			</div>
		</div>
	</form>
</Modal>

<!-- Grant App Access Modal -->
<Modal title="Grant Application Access" open={showAppAccessModal} onClose={() => (showAppAccessModal = false)}>
	<form onsubmit={(e) => { e.preventDefault(); submitGrantAppAccess(); }}>
		<div class="space-y-4">
			<div>
				<label class="block text-sm font-medium text-gray-700 mb-1">Application</label>
				<select bind:value={appAccessForm.applicationId} class="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 bg-white">
					{#each applications as a}<option value={a.id}>{a.name} ({a.clientName})</option>{/each}
				</select>
			</div>
			<div>
				<label class="block text-sm font-medium text-gray-700 mb-1">Role</label>
				<select bind:value={appAccessForm.role} class="w-full border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 bg-white">
					{#each appRoles as r}<option value={r}>{r}</option>{/each}
				</select>
			</div>
			<div class="flex justify-end gap-3 pt-1">
				<button type="button" onclick={() => (showAppAccessModal = false)} class="px-4 py-2 text-sm text-gray-700 border border-gray-300 rounded-md hover:bg-gray-50">Cancel</button>
				<button type="submit" disabled={saving} class="px-4 py-2 text-sm text-white bg-indigo-600 rounded-md hover:bg-indigo-700 disabled:opacity-50">
					{saving ? 'Granting...' : 'Grant Access'}
				</button>
			</div>
		</div>
	</form>
</Modal>
