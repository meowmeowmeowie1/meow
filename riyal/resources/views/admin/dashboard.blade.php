@extends('admin.layout')

@section('admin')
<h1 class="text-2xl font-bold">Dashboard</h1>

<div class="mt-6 grid grid-cols-2 gap-4 md:grid-cols-3 lg:grid-cols-6">
    @foreach ([
        'Posts' => $stats['posts'],
        'Published' => $stats['published'],
        'Drafts' => $stats['drafts'],
        'Categories' => $stats['categories'],
        'Tags' => $stats['tags'],
        'Views' => $stats['views'],
    ] as $label => $value)
        <div class="rounded-xl border border-gray-200 bg-white p-4">
            <div class="text-2xl font-bold text-gray-900">{{ number_format($value) }}</div>
            <div class="text-xs uppercase tracking-wide text-gray-500">{{ $label }}</div>
        </div>
    @endforeach
</div>

<div class="mt-8 flex items-center justify-between">
    <h2 class="text-lg font-semibold">Recent posts</h2>
    <a href="{{ route('admin.posts.create') }}" class="rounded-lg bg-brand-600 px-4 py-2 text-sm font-medium text-white hover:bg-brand-700">+ New post</a>
</div>

<div class="mt-4 overflow-hidden rounded-xl border border-gray-200 bg-white">
    <table class="w-full text-sm">
        <thead class="bg-gray-50 text-start text-xs uppercase text-gray-500">
            <tr>
                <th class="px-4 py-3 text-start">Title</th>
                <th class="px-4 py-3 text-start">Locale</th>
                <th class="px-4 py-3 text-start">Status</th>
                <th class="px-4 py-3 text-start">Author</th>
            </tr>
        </thead>
        <tbody class="divide-y divide-gray-100">
            @forelse ($recent as $post)
                <tr>
                    <td class="px-4 py-3"><a href="{{ route('admin.posts.edit', $post) }}" class="font-medium text-brand-700 hover:underline">{{ $post->title }}</a></td>
                    <td class="px-4 py-3 uppercase">{{ $post->locale }}</td>
                    <td class="px-4 py-3">{{ $post->status }}</td>
                    <td class="px-4 py-3">{{ $post->author?->name }}</td>
                </tr>
            @empty
                <tr><td colspan="4" class="px-4 py-6 text-center text-gray-500">No posts yet.</td></tr>
            @endforelse
        </tbody>
    </table>
</div>
@endsection
