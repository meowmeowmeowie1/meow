@extends('admin.layout')

@section('admin')
<div class="flex items-center justify-between">
    <h1 class="text-2xl font-bold">Posts</h1>
    <a href="{{ route('admin.posts.create') }}" class="rounded-lg bg-brand-600 px-4 py-2 text-sm font-medium text-white hover:bg-brand-700">+ New post</a>
</div>

<form method="get" class="mt-4 flex gap-2 text-sm">
    <select name="locale" class="rounded-lg border border-gray-300 px-3 py-1.5" onchange="this.form.submit()">
        <option value="">All locales</option>
        <option value="ar" @selected(request('locale')==='ar')>AR</option>
        <option value="en" @selected(request('locale')==='en')>EN</option>
    </select>
    <select name="status" class="rounded-lg border border-gray-300 px-3 py-1.5" onchange="this.form.submit()">
        <option value="">All statuses</option>
        <option value="published" @selected(request('status')==='published')>Published</option>
        <option value="draft" @selected(request('status')==='draft')>Draft</option>
    </select>
</form>

<div class="mt-4 overflow-hidden rounded-xl border border-gray-200 bg-white">
    <table class="w-full text-sm">
        <thead class="bg-gray-50 text-xs uppercase text-gray-500">
            <tr>
                <th class="px-4 py-3 text-start">Title</th>
                <th class="px-4 py-3 text-start">Locale</th>
                <th class="px-4 py-3 text-start">Category</th>
                <th class="px-4 py-3 text-start">Status</th>
                <th class="px-4 py-3 text-start">Linked?</th>
                <th class="px-4 py-3 text-start">Actions</th>
            </tr>
        </thead>
        <tbody class="divide-y divide-gray-100">
            @forelse ($posts as $post)
                <tr>
                    <td class="px-4 py-3"><a href="{{ route('admin.posts.edit', $post) }}" class="font-medium text-brand-700 hover:underline">{{ $post->title }}</a></td>
                    <td class="px-4 py-3 uppercase">{{ $post->locale }}</td>
                    <td class="px-4 py-3">{{ $post->category?->name ?? '—' }}</td>
                    <td class="px-4 py-3">
                        <span class="rounded-full px-2 py-0.5 text-xs {{ $post->status === 'published' ? 'bg-green-100 text-green-700' : 'bg-gray-100 text-gray-600' }}">{{ $post->status }}</span>
                    </td>
                    <td class="px-4 py-3">{{ $post->translation_group_id ? '✓' : '—' }}</td>
                    <td class="px-4 py-3">
                        <form action="{{ route('admin.posts.destroy', $post) }}" method="post" onsubmit="return confirm('Delete this post?')">
                            @csrf @method('DELETE')
                            <button class="text-xs text-red-600 hover:underline">Delete</button>
                        </form>
                    </td>
                </tr>
            @empty
                <tr><td colspan="6" class="px-4 py-6 text-center text-gray-500">No posts found.</td></tr>
            @endforelse
        </tbody>
    </table>
</div>

<div class="mt-6">{{ $posts->links() }}</div>
@endsection
