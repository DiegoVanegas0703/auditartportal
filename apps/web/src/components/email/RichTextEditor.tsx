import Link from '@tiptap/extension-link'
import Placeholder from '@tiptap/extension-placeholder'
import TextAlign from '@tiptap/extension-text-align'
import Underline from '@tiptap/extension-underline'
import { EditorContent, useEditor } from '@tiptap/react'
import StarterKit from '@tiptap/starter-kit'
import {
  AlignCenter,
  AlignLeft,
  AlignRight,
  Bold,
  Italic,
  Link2,
  List,
  ListOrdered,
  Redo2,
  Underline as UnderlineIcon,
  Undo2,
} from 'lucide-react'
import { useEffect, type ReactNode } from 'react'

function htmlToPlainText(html: string): string {
  const doc = new DOMParser().parseFromString(html, 'text/html')
  return (doc.body.textContent ?? '').replace(/\u00a0/g, ' ').trim()
}

export function RichTextEditor({
  valueHtml,
  onChange,
  placeholder = 'Escribí el mensaje…',
  minHeightClass = 'min-h-[140px]',
}: {
  valueHtml: string
  onChange: (next: { html: string; text: string }) => void
  placeholder?: string
  minHeightClass?: string
}) {
  const editor = useEditor({
    immediatelyRender: false,
    extensions: [
      StarterKit,
      Underline,
      Link.configure({
        openOnClick: false,
        HTMLAttributes: { class: 'text-auditart-blue underline' },
      }),
      TextAlign.configure({ types: ['heading', 'paragraph'] }),
      Placeholder.configure({ placeholder }),
    ],
    content: valueHtml || '',
    onUpdate: ({ editor: ed }) => {
      const html = ed.getHTML()
      const empty = ed.isEmpty
      onChange({
        html: empty ? '' : html,
        text: empty ? '' : htmlToPlainText(html),
      })
    },
    editorProps: {
      attributes: {
        class: `prose prose-sm max-w-none px-3 py-2 text-sm text-auditart-navy focus:outline-none ${minHeightClass}`,
      },
    },
  })

  useEffect(() => {
    if (!editor) return
    const current = editor.getHTML()
    const incoming = valueHtml || ''
    if (incoming === current) return
    if (!incoming && editor.isEmpty) return
    editor.commands.setContent(incoming, { emitUpdate: false })
  }, [editor, valueHtml])

  if (!editor) return null

  const setLink = () => {
    const prev = editor.getAttributes('link').href as string | undefined
    const url = window.prompt('URL del enlace', prev ?? 'https://')
    if (url === null) return
    if (!url.trim()) {
      editor.chain().focus().extendMarkRange('link').unsetLink().run()
      return
    }
    editor.chain().focus().extendMarkRange('link').setLink({ href: url.trim() }).run()
  }

  return (
    <div className="overflow-hidden rounded-xl border border-gray-200 bg-white">
      <div className="flex flex-wrap gap-0.5 border-b border-gray-100 bg-gray-50/80 px-2 py-1.5">
        <ToolbarBtn
          active={editor.isActive('bold')}
          onClick={() => editor.chain().focus().toggleBold().run()}
          title="Negrita"
        >
          <Bold size={14} />
        </ToolbarBtn>
        <ToolbarBtn
          active={editor.isActive('italic')}
          onClick={() => editor.chain().focus().toggleItalic().run()}
          title="Cursiva"
        >
          <Italic size={14} />
        </ToolbarBtn>
        <ToolbarBtn
          active={editor.isActive('underline')}
          onClick={() => editor.chain().focus().toggleUnderline().run()}
          title="Subrayado"
        >
          <UnderlineIcon size={14} />
        </ToolbarBtn>
        <span className="mx-1 w-px self-stretch bg-gray-200" />
        <ToolbarBtn
          active={editor.isActive('bulletList')}
          onClick={() => editor.chain().focus().toggleBulletList().run()}
          title="Lista"
        >
          <List size={14} />
        </ToolbarBtn>
        <ToolbarBtn
          active={editor.isActive('orderedList')}
          onClick={() => editor.chain().focus().toggleOrderedList().run()}
          title="Lista numerada"
        >
          <ListOrdered size={14} />
        </ToolbarBtn>
        <span className="mx-1 w-px self-stretch bg-gray-200" />
        <ToolbarBtn
          active={editor.isActive({ textAlign: 'left' })}
          onClick={() => editor.chain().focus().setTextAlign('left').run()}
          title="Izquierda"
        >
          <AlignLeft size={14} />
        </ToolbarBtn>
        <ToolbarBtn
          active={editor.isActive({ textAlign: 'center' })}
          onClick={() => editor.chain().focus().setTextAlign('center').run()}
          title="Centrar"
        >
          <AlignCenter size={14} />
        </ToolbarBtn>
        <ToolbarBtn
          active={editor.isActive({ textAlign: 'right' })}
          onClick={() => editor.chain().focus().setTextAlign('right').run()}
          title="Derecha"
        >
          <AlignRight size={14} />
        </ToolbarBtn>
        <span className="mx-1 w-px self-stretch bg-gray-200" />
        <ToolbarBtn active={editor.isActive('link')} onClick={setLink} title="Enlace">
          <Link2 size={14} />
        </ToolbarBtn>
        <span className="mx-1 w-px self-stretch bg-gray-200" />
        <ToolbarBtn
          onClick={() => editor.chain().focus().undo().run()}
          title="Deshacer"
          disabled={!editor.can().undo()}
        >
          <Undo2 size={14} />
        </ToolbarBtn>
        <ToolbarBtn
          onClick={() => editor.chain().focus().redo().run()}
          title="Rehacer"
          disabled={!editor.can().redo()}
        >
          <Redo2 size={14} />
        </ToolbarBtn>
      </div>
      <EditorContent editor={editor} />
    </div>
  )
}

function ToolbarBtn({
  children,
  onClick,
  active,
  disabled,
  title,
}: {
  children: ReactNode
  onClick: () => void
  active?: boolean
  disabled?: boolean
  title: string
}) {
  return (
    <button
      type="button"
      title={title}
      disabled={disabled}
      onClick={onClick}
      className={`rounded-lg p-1.5 transition-colors disabled:opacity-40 ${
        active
          ? 'bg-auditart-blue/15 text-auditart-blue'
          : 'text-auditart-gray hover:bg-white hover:text-auditart-navy'
      }`}
    >
      {children}
    </button>
  )
}
